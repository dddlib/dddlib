CREATE TABLE [dbo].[DispatchedEvents] (
    [DispatcherId] UNIQUEIDENTIFIER,
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
    [DispatcherId] UNIQUEIDENTIFIER,
    [SequenceNumber] BIGINT NOT NULL,
    [Size] INT NOT NULL,
    [Timestamp] DATETIME2 (7) DEFAULT (GETDATE()) NOT NULL,
    [Complete]  BIT DEFAULT (0) NOT NULL
);
GO

ALTER PROCEDURE [dbo].[MonitorUndispatchedEvents]
    @SequenceNumber BIGINT
AS

SELECT [SequenceNumber]
FROM [dbo].[Events]
WHERE [SequenceNumber] > @SequenceNumber
ORDER BY [SequenceNumber] DESC;

GO

ALTER PROCEDURE [dbo].[MonitorUndispatchedBatches]
    @DispatcherId UNIQUEIDENTIFIER
AS

SELECT [Id]
FROM [dbo].[Batches]
WHERE [DispatcherId] = @DispatcherId
    AND [Complete] = 0
ORDER BY [SequenceNumber] ASC;

GO

ALTER PROCEDURE [dbo].[GetNextUndispatchedEventsBatch]
    @DispatcherId UNIQUEIDENTIFIER,
    @MaxBatchSize INT
AS

SET NOCOUNT ON;

UPDATE [dbo].[Batches]
SET [Complete] = 1
WHERE [DispatcherId] = @DispatcherId
    AND [Complete] = 0
    AND [SequenceNumber] >= (
        SELECT MIN([SequenceNumber])
        FROM [dbo].[Batches]
        WHERE [DispatcherId] = @DispatcherId
            AND [Complete] = 0
            AND DATEDIFF(ss, [Timestamp], GETDATE()) >= 30);

BEGIN TRANSACTION;

WITH BatchedEvents
AS
(
    SELECT MAX([SequenceNumber] + [Size] - 1) AS [SequenceNumber]
    FROM [dbo].[Batches]
    WHERE [DispatcherId] = @DispatcherId
        AND [Complete] = 0
), UnDispatchedEvents
AS
(
    SELECT MAX([SequenceNumber]) AS [SequenceNumber]
    FROM [dbo].[Events]
), DispatchedEvents
AS
(
    SELECT MAX([SequenceNumber]) AS [SequenceNumber]
    FROM [dbo].[DispatchedEvents]
    WHERE [DispatcherId] = @DispatcherId
)
INSERT INTO [dbo].[Batches] ([DispatcherId], [SequenceNumber], [Size])
SELECT
    @DispatcherId,
    COALESCE([Batched].[SequenceNumber], [Dispatched].[SequenceNumber], 0) + 1 AS [SequenceNumber],
    IIF([Undispatched].[SequenceNumber] - COALESCE([Batched].[SequenceNumber], [Dispatched].[SequenceNumber], 0) > @MaxBatchSize, @MaxBatchSize, [Undispatched].[SequenceNumber] - COALESCE([Batched].[SequenceNumber], [Dispatched].[SequenceNumber], 0)) AS [Size]
FROM BatchedEvents [Batched] CROSS JOIN UnDispatchedEvents [Undispatched] CROSS JOIN DispatchedEvents [Dispatched]
WHERE
    IIF([Undispatched].[SequenceNumber] - COALESCE([Batched].[SequenceNumber], [Dispatched].[SequenceNumber], 0) > @MaxBatchSize, @MaxBatchSize, [Undispatched].[SequenceNumber] - COALESCE([Batched].[SequenceNumber], [Dispatched].[SequenceNumber], 0)) > 0;

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
    @DispatcherId UNIQUEIDENTIFIER,
    @SequenceNumber BIGINT
AS

SET NOCOUNT ON;

MERGE INTO [dbo].[DispatchedEvents] AS [Target]
USING (SELECT @DispatcherId AS [DispatcherId], @SequenceNumber AS [SequenceNumber]) AS [Source]
ON [Target].[DispatcherId] = [Source].[DispatcherId]
WHEN MATCHED THEN
    UPDATE
    SET [Target].[SequenceNumber] = [Source].[SequenceNumber]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([DispatcherId], [SequenceNumber])
    VALUES ([Source].[DispatcherId], [Source].[SequenceNumber]);

WITH BatchedEvents
AS
(
    SELECT [Id], SUM([SequenceNumber] + [Size] - 1) AS [SequenceNumber]
    FROM [dbo].[Batches]
    WHERE [DispatcherId] = @DispatcherId
        AND [Complete] = 0
    GROUP BY [Id]
)
UPDATE [Batch]
SET [Batch].[Complete] = 1
FROM [dbo].[Batches] [Batch] INNER JOIN BatchedEvents [Batched] ON [Batch].[Id] = [Batched].[Id]
WHERE [Batched].[SequenceNumber] = @SequenceNumber;

GO