-- NOTE (Cameron): The SQL comments are required for the alternate execution method for this script (via .NET)
IF EXISTS (SELECT * FROM information_schema.tables WHERE table_name='DatabaseVersion' AND table_schema = 'dbo') SET NOEXEC ON

-- LINK (Cameron): http://stackoverflow.com/questions/4443262/tsql-add-column-to-table-and-then-update-it-inside-transaction-go
SET XACT_ABORT ON
GO

BEGIN TRANSACTION
GO

-- SQL: Creating the 'events' table
CREATE TABLE [dbo].[Events] (
    [Id] BIGINT IDENTITY (1, 1) NOT NULL,
    [Payload] VARCHAR (50) NOT NULL,
    [Dispatched] BIT DEFAULT ((0)) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

IF XACT_STATE() < 1 SET NOEXEC ON
GO

-- SQL: Creating the 'batches' table
CREATE TABLE [dbo].[Batches] (
    [ID] BIGINT IDENTITY (1, 1) NOT NULL,
    [EventId] BIGINT NOT NULL,
    [Size] INT NOT NULL,
    [Timestamp] DATETIME2 (7) DEFAULT (GETDATE()) NOT NULL,
    [Complete]  BIT DEFAULT (0) NOT NULL
);
GO

IF XACT_STATE() < 1 SET NOEXEC ON
GO

-- SQL: Creating the 'monitor undispatched events' stored procedure
CREATE PROCEDURE [dbo].[MonitorUndispatchedEvents]
AS

SELECT [Id] as [EventId]
FROM [dbo].[Events]
WHERE [Dispatched] = 0
ORDER BY [Id] DESC;

GO

IF XACT_STATE() < 1 SET NOEXEC ON
GO

-- SQL: Creating the 'monitor undispatched batches' stored procedure
CREATE PROCEDURE [dbo].[MonitorUndispatchedBatches]
AS

SELECT [Id] AS [BatchId]
FROM [dbo].[Batches]
WHERE [Complete] = 0
ORDER BY [EventId]

GO

IF XACT_STATE() < 1 SET NOEXEC ON
GO

-- SQL: Creating the 'get next undispatched events batch' stored procedure
CREATE PROCEDURE [dbo].[GetNextUndispatchedEventsBatch]
    @MaxBatchSize int
AS

SET NOCOUNT ON;

UPDATE [dbo].[Batches]
SET [Complete] = 1
WHERE [EventId] >= (
    SELECT MIN([EventId])
    FROM [dbo].[Batches]
    WHERE [Complete] = 0
        AND DATEDIFF(ss, [Timestamp], GETDATE()) >= 30);

BEGIN TRANSACTION;

WITH IncompleteBatches
AS
(
    SELECT [Id] AS [BatchId], [EventId], [Size]
    FROM [dbo].[Batches]
    WHERE [Complete] = 0
), UnDispatchedEvents
AS
(
    SELECT [Event].[Id] AS [EventId], [Batch].[BatchId]
    FROM [dbo].[Events] [Event] LEFT OUTER JOIN IncompleteBatches [Batch] ON ([Event].[Id] >= [Batch].[EventId] AND [Event].[Id] < [Batch].[EventId] + [Batch].[Size])
    WHERE [Event].[Dispatched] = 0
), NextUndispatchedEvents
AS
(
    SELECT TOP (@MaxBatchSize) [EventId]
    FROM UnDispatchedEvents
    WHERE [BatchId] IS NULL
), NextBatch
AS
(
    SELECT MIN([EventId]) AS [EventId], MAX([EventId]) - MIN([EventId]) + 1 AS [Size]
    FROM NextUndispatchedEvents
)
INSERT INTO [dbo].[Batches] ([EventId], [Size])
SELECT [EventId], [Size]
FROM NextBatch
WHERE [EventId] IS NOT NULL;

COMMIT;

DECLARE @BatchId BIGINT = SCOPE_IDENTITY();

SELECT [Id] AS [BatchId]
FROM [dbo].[Batches]
WHERE [Id] = @BatchId;

SELECT [Event].[Id] AS [EventId], [Event].[Payload]
FROM [dbo].[Events] [Event] INNER JOIN (
    SELECT [Id] AS [BatchId], [EventId], [Size]
    FROM [dbo].[Batches]
    WHERE [Id] = @BatchId) [Batch] ON ([Event].[Id] >= [Batch].[EventId] AND [Event].[Id] < [Batch].[EventId] + [Batch].[Size])

GO

IF XACT_STATE() < 1 SET NOEXEC ON
GO

-- SQL: Creating the 'mark events as dispatched' stored procedure
CREATE PROCEDURE [dbo].[MarkEventAsDispatched]
    @EventId bigint
AS

SET NOCOUNT ON;

UPDATE [dbo].[Events]
SET [Dispatched] = 1
WHERE [Id] = @EventId;

WITH IncompleteBatches
AS
(
    SELECT [Id] AS [BatchId], [EventId], [Size]
    FROM [dbo].[Batches]
    WHERE [Complete] = 0
), UnDispatchedEvents
AS
(
    SELECT [Batch].[BatchId], [Event].[Dispatched], COUNT([Event].[Dispatched]) AS [Count]
    FROM [dbo].[Events] [Event] LEFT OUTER JOIN IncompleteBatches [Batch] ON ([Event].[Id] >= [Batch].[EventId] AND [Event].[Id] < [Batch].[EventId] + [Batch].[Size])
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

IF XACT_STATE() < 1 SET NOEXEC ON
GO

SET NOCOUNT ON;

-- SQL: Assigning the database version as the initial version
INSERT INTO [dbo].[DatabaseVersion] ([Id], [Description])
SELECT 1, 'Initial version';
GO

SET NOEXEC OFF
GO

IF XACT_STATE() = 1 COMMIT
GO