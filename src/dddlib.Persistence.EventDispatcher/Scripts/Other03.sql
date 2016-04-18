CREATE TABLE [dbo].[DispatchedEvents] (
    [DispatcherId] UNIQUEIDENTIFIER,
    [SequenceNumber] BIGINT NOT NULL
);
GO

INSERT INTO [dbo].[DispatchedEvents]
SELECT NULL AS [DispatcherId], MAX([SequenceNumber]) AS [SequenceNumber], 1 AS [Complete]
FROM [dbo].[Events]
WHERE [Dispatched] = 1;

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
    AND [SequenceNumber] >= (
        SELECT MIN([SequenceNumber])
        FROM [dbo].[Batches]
        WHERE [DispatcherId] = @DispatcherId
            AND [Complete] = 0
            AND DATEDIFF(ss, [Timestamp], GETDATE()) >= 30);

BEGIN TRANSACTION;

INSERT INTO [dbo].[Batches] ([DispatcherId], [SequenceNumber], [Size])
SELECT
    NULL as [DispatcherId],
    MAX([Dispatched].[SequenceNumber]) + 1 AS [SequenceNumber],
    IIF(MAX([Event].[SequenceNumber]) - MAX([Dispatched].[SequenceNumber]) > @MaxBatchSize, @MaxBatchSize, MAX([Event].[SequenceNumber]) - MAX([Dispatched].[SequenceNumber])) AS [Size]
FROM [dbo].[Events] [Event] CROSS JOIN [dbo].[DispatchedEvents] [Dispatched]
WHERE [Dispatched].[DispatcherId] IS NULL

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

UPDATE [dbo].[DispatchedEvents]
SET [SequenceNumber] = @SequenceNumber
WHERE [DispatcherId] = @DispatcherId;

WITH IncompleteBatches
AS
(
    SELECT [Id] AS [BatchId], [SequenceNumber], [Size]
    FROM [dbo].[Batches]
    WHERE [Complete] = 0
), UnDispatchedEvents
AS
(
    SELECT [Batch].[BatchId], [Event].[Dispatched], COUNT([Event].[Dispatched]) AS [Count]
    FROM [dbo].[Events] [Event] LEFT OUTER JOIN IncompleteBatches [Batch] ON ([Event].[SequenceNumber] >= [Batch].[SequenceNumber] AND [Event].[SequenceNumber] < [Batch].[SequenceNumber] + [Batch].[Size])
    WHERE [Batch].[BatchId] IS NOT NULL
    GROUP BY [Batch].[BatchId], [Event].[Dispatched]
), PendingDispatch
AS
(
    SELECT [BatchId], ISNULL([0], 0) AS [Pending], ISNULL([1], 0) AS Dispatched
    FROM UnDispatchedEvents
    PIVOT
    (
      SUM([Count])
      for [Dispatched] in ([0], [1])
    ) [Results]
)
UPDATE [Batch]
SET [Batch].[Complete] = 1
FROM PendingDispatch [Pending] INNER JOIN [dbo].[Batches] [Batch] ON [Pending].[BatchId] = [Batch].[Id]
WHERE [Pending].[Pending] = 0;

GO