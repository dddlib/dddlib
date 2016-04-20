CREATE TABLE [dbo].[DispatchedEvents] (
    [DispatcherId] VARCHAR(10),
    [SequenceNumber] BIGINT NOT NULL
);
GO

INSERT INTO [dbo].[DispatchedEvents]
SELECT NULL AS [DispatcherId], MAX([SequenceNumber]) AS [SequenceNumber]
FROM [dbo].[Events]
WHERE [Dispatched] = 1
HAVING MAX([SequenceNumber]) IS NOT NULL;

GO

DECLARE @Sql VARCHAR(MAX);
SELECT @Sql = 'ALTER TABLE [dbo].[Events] DROP CONSTRAINT [' + dc.Name + '];'
FROM sys.tables t
    INNER JOIN sys.default_constraints dc ON t.[object_id] = dc.[parent_object_id]
    INNER JOIN sys.columns c ON dc.[parent_object_id] = c.[object_id] AND c.[column_id] = dc.[parent_column_id]
    INNER JOIN sys.schemas s on t.[schema_id] = s.[schema_id]
WHERE s.name = 'dbo'
    AND t.name = 'Events'
    AND c.name = 'Dispatched';

EXEC(@Sql);
GO

ALTER TABLE [dbo].[Events]
DROP COLUMN [Dispatched];
GO

DROP TABLE [dbo].[Batches];
GO

CREATE TABLE [dbo].[Batches] (
    [Id] BIGINT IDENTITY (1, 1) NOT NULL,
    [DispatcherId] VARCHAR(10),
    [SequenceNumber] BIGINT NOT NULL,
    [Size] INT NOT NULL,
    [Timestamp] DATETIME2 (7) DEFAULT (GETDATE()) NOT NULL,
    [Complete]  BIT DEFAULT (0) NOT NULL
);
GO

ALTER PROCEDURE [dbo].[MonitorUndispatchedBatches]
    @DispatcherId VARCHAR(10)
AS

SELECT [Id]
FROM [dbo].[Batches]
WHERE ([DispatcherId] = @DispatcherId OR [DispatcherId] IS NULL AND @DispatcherId IS NULL)
    AND [Complete] = 0
ORDER BY [SequenceNumber] ASC;

GO

ALTER PROCEDURE [dbo].[GetNextUndispatchedEventsBatch]
    @DispatcherId VARCHAR(10),
    @MaxBatchSize INT
AS

SET NOCOUNT ON;

UPDATE [dbo].[Batches]
SET [Complete] = 1
WHERE ([DispatcherId] = @DispatcherId OR [DispatcherId] IS NULL AND @DispatcherId IS NULL)
    AND [Complete] = 0
    AND [SequenceNumber] >= (
        SELECT MIN([SequenceNumber])
        FROM [dbo].[Batches]
        WHERE ([DispatcherId] = @DispatcherId OR [DispatcherId] IS NULL AND @DispatcherId IS NULL)
            AND [Complete] = 0
            AND DATEDIFF(ss, [Timestamp], GETDATE()) >= 30);

BEGIN TRANSACTION;

WITH BatchedEvents
AS
(
    SELECT MAX([SequenceNumber] + [Size] - 1) AS [SequenceNumber]
    FROM [dbo].[Batches]
    WHERE ([DispatcherId] = @DispatcherId OR [DispatcherId] IS NULL AND @DispatcherId IS NULL)
        AND [Complete] = 0
), UnDispatchedEvents
AS
(
    SELECT MAX([SequenceNumber]) AS [SequenceNumber]
    FROM [dbo].[Events]
), DispatchedEvents
AS
(
    SELECT [SequenceNumber]
    FROM [dbo].[DispatchedEvents]
    WHERE ([DispatcherId] = @DispatcherId OR [DispatcherId] IS NULL AND @DispatcherId IS NULL)
)
INSERT INTO [dbo].[Batches] ([SequenceNumber], [Size])
SELECT
    ISNULL([Batched].[SequenceNumber], [Dispatched].[SequenceNumber]) + 1 AS [SequenceNumber],
    IIF([Undispatched].[SequenceNumber] - ISNULL([Batched].[SequenceNumber], [Dispatched].[SequenceNumber]) > @MaxBatchSize, @MaxBatchSize, [Undispatched].[SequenceNumber] - ISNULL([Batched].[SequenceNumber], [Dispatched].[SequenceNumber])) AS [Size]
FROM BatchedEvents [Batched] CROSS JOIN UnDispatchedEvents [Undispatched] CROSS JOIN DispatchedEvents [Dispatched]
WHERE
    IIF([Undispatched].[SequenceNumber] - ISNULL([Batched].[SequenceNumber], [Dispatched].[SequenceNumber]) > @MaxBatchSize, @MaxBatchSize, [Undispatched].[SequenceNumber] - ISNULL([Batched].[SequenceNumber], [Dispatched].[SequenceNumber])) > 0;

COMMIT;

DECLARE @BatchId BIGINT = SCOPE_IDENTITY();

SELECT [Id] AS [BatchId]
FROM [dbo].[Batches]
WHERE [Id] = @BatchId;

SELECT [Event].[SequenceNumber], [Type].[Name] AS [PayloadTypeName], [Event].[Payload]
FROM [dbo].[Events] [Event] WITH (NOLOCK) INNER JOIN (
    SELECT [Id] AS [BatchId], [SequenceNumber], [Size]
    FROM [dbo].[Batches]
    WHERE [Id] = @BatchId) [Batch] ON ([Event].[SequenceNumber] >= [Batch].[SequenceNumber] AND [Event].[SequenceNumber] < [Batch].[SequenceNumber] + [Batch].[Size])
    INNER JOIN [dbo].[Types] [Type] ON [Event].[TypeId] = [Type].[Id];

GO

ALTER PROCEDURE [dbo].[MarkEventAsDispatched]
    @DispatcherId VARCHAR(10),
    @SequenceNumber BIGINT
AS

SET NOCOUNT ON;

UPDATE [dbo].[DispatchedEvents]
SET [SequenceNumber] = @SequenceNumber
WHERE ([DispatcherId] = @DispatcherId OR [DispatcherId] IS NULL AND @DispatcherId IS NULL);

WITH BatchedEvents
AS
(
    SELECT [Id], SUM([SequenceNumber] + [Size] - 1) AS [SequenceNumber]
    FROM [dbo].[Batches]
    WHERE ([DispatcherId] = @DispatcherId OR [DispatcherId] IS NULL AND @DispatcherId IS NULL)
        AND [Complete] = 0
    GROUP BY [Id]
)
UPDATE [Batch]
SET [Batch].[Complete] = 1
FROM [dbo].[Batches] [Batch] INNER JOIN BatchedEvents [Batched] ON [Batch].[Id] = [Batched].[Id]
WHERE [Batched].[SequenceNumber] = @SequenceNumber;

GO