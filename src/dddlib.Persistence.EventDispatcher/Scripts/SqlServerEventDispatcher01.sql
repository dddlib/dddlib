DECLARE @Sql NVARCHAR(MAX) = N'ALTER DATABASE [' + db_name() + '] SET ENABLE_BROKER WITH ROLLBACK IMMEDIATE';
EXECUTE sp_executesql @Sql;
GO

ALTER TABLE [dbo].[Events]
ADD [Dispatched] BIT NOT NULL DEFAULT(0);
GO

CREATE TABLE [dbo].[Batches] (
    [Id] BIGINT IDENTITY (1, 1) NOT NULL,
    [SequenceNumber] BIGINT NOT NULL,
    [Size] INT NOT NULL,
    [Timestamp] DATETIME2 (7) DEFAULT (GETDATE()) NOT NULL,
    [Complete]  BIT DEFAULT (0) NOT NULL
);
GO

CREATE PROCEDURE [dbo].[MonitorUndispatchedEvents]
AS

SELECT [SequenceNumber]
FROM [dbo].[Events]
WHERE [Dispatched] = 0
ORDER BY [SequenceNumber] ASC;

GO

CREATE PROCEDURE [dbo].[MonitorUndispatchedBatches]
AS

SELECT [SequenceNumber]
FROM [dbo].[Batches]
WHERE [Complete] = 0
ORDER BY [SequenceNumber]

GO

CREATE PROCEDURE [dbo].[GetNextUndispatchedEventsBatch]
    @MaxBatchSize int
AS

SET NOCOUNT ON;

UPDATE [dbo].[Batches]
SET [Complete] = 1
WHERE [SequenceNumber] >= (
    SELECT MIN([SequenceNumber])
    FROM [dbo].[Batches]
    WHERE [Complete] = 0
        AND DATEDIFF(ss, [Timestamp], GETDATE()) >= 30);

BEGIN TRANSACTION;

WITH IncompleteBatches
AS
(
    SELECT [Id] AS [BatchId], [SequenceNumber], [Size]
    FROM [dbo].[Batches]
    WHERE [Complete] = 0
), UnDispatchedEvents
AS
(
    SELECT [Event].[SequenceNumber], [Batch].[BatchId]
    FROM [dbo].[Events] [Event] LEFT OUTER JOIN IncompleteBatches [Batch] ON ([Event].[SequenceNumber] >= [Batch].[SequenceNumber] AND [Event].[SequenceNumber] < [Batch].[SequenceNumber] + [Batch].[Size])
    WHERE [Event].[Dispatched] = 0
), NextUndispatchedEvents
AS
(
    SELECT TOP (@MaxBatchSize) [SequenceNumber]
    FROM UnDispatchedEvents
    WHERE [BatchId] IS NULL
), NextBatch
AS
(
    SELECT MIN([SequenceNumber]) AS [SequenceNumber], MAX([SequenceNumber]) - MIN([SequenceNumber]) + 1 AS [Size]
    FROM NextUndispatchedEvents
)
INSERT INTO [dbo].[Batches] ([SequenceNumber], [Size])
SELECT [SequenceNumber], [Size]
FROM NextBatch
WHERE [SequenceNumber] IS NOT NULL;

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

CREATE PROCEDURE [dbo].[MarkEventAsDispatched]
    @SequenceNumber bigint
AS

SET NOCOUNT ON;

UPDATE [dbo].[Events]
SET [Dispatched] = 1
WHERE [SequenceNumber] = @SequenceNumber;

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

CREATE PROCEDURE [dbo].[GetEventsFrom]
    @SequenceNumber BIGINT
AS

SELECT [Event].[SequenceNumber], [Type].[Name] AS [PayloadTypeName], [Event].[Payload]
FROM [dbo].[Events] [Event] WITH (NOLOCK)INNER JOIN [dbo].[Types] [Type] ON [Event].[TypeId] = [Type].[Id]
WHERE [Event].[SequenceNumber] > @SequenceNumber
ORDER BY [Event].[SequenceNumber];

GO