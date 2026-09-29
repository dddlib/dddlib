-- dddlib.Persistence.EventDispatcher: batches of undispatched events per dispatcher.
-- Requires the dddlib.Persistence scripts (01 and 03).

IF OBJECT_ID('[dbo].[Batches]') IS NULL
CREATE TABLE [dbo].[Batches]
(
    [Id] BIGINT IDENTITY NOT NULL,
    [DispatcherId] UNIQUEIDENTIFIER NOT NULL,
    [SequenceNumber] BIGINT NOT NULL,
    [LastSequenceNumber] BIGINT NOT NULL,
    [Timestamp] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [Complete] BIT NOT NULL DEFAULT 0,
    CONSTRAINT [PK_Batch] PRIMARY KEY CLUSTERED ([Id])
);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Batches]') AND name = N'IX_Batch_Dispatcher')
CREATE INDEX [IX_Batch_Dispatcher] ON [dbo].[Batches] ([DispatcherId], [Complete]) INCLUDE ([LastSequenceNumber], [Timestamp]);
GO

IF OBJECT_ID('[dbo].[DispatchedEvents]') IS NULL
CREATE TABLE [dbo].[DispatchedEvents]
(
    [DispatcherId] UNIQUEIDENTIFIER NOT NULL,
    [SequenceNumber] BIGINT NOT NULL,
    CONSTRAINT [PK_DispatchedEvent] PRIMARY KEY CLUSTERED ([DispatcherId])
);
GO

CREATE OR ALTER PROCEDURE [dbo].[GetNextBatch]
    @DispatcherId UNIQUEIDENTIFIER,
    @MaxBatchSize INT,
    @BatchTimeoutMilliseconds INT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Lock INT;
DECLARE @First BIGINT;
DECLARE @Last BIGINT;
DECLARE @BatchId BIGINT;

BEGIN TRANSACTION;

    EXEC @Lock = sp_getapplock @Resource = @DispatcherId, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire the batch lock for the dispatcher.', 1;

    -- Batches that were never completed are handed out again after the timeout. The comparison is at millisecond
    -- precision: DATEDIFF(SECOND) counts second boundaries crossed, which would expire a batch taken at xx.999 at (xx+1).001.
    UPDATE [dbo].[Batches]
    SET [Complete] = 1
    WHERE [DispatcherId] = @DispatcherId
        AND [Complete] = 0
        AND [Timestamp] <= DATEADD(MILLISECOND, -@BatchTimeoutMilliseconds, SYSUTCDATETIME());

    DECLARE @After BIGINT = (
        SELECT MAX([SequenceNumber])
        FROM (
            SELECT COALESCE(MAX([LastSequenceNumber]), 0) AS [SequenceNumber] FROM [dbo].[Batches] WHERE [DispatcherId] = @DispatcherId AND [Complete] = 0
            UNION ALL
            SELECT COALESCE(MAX([SequenceNumber]), 0) FROM [dbo].[DispatchedEvents] WHERE [DispatcherId] = @DispatcherId
        ) AS [Positions]);

    -- The batch is bounded by actual events, so gaps left by rolled-back commits are skipped.
    SELECT @First = MIN([SequenceNumber]), @Last = MAX([SequenceNumber])
    FROM (
        SELECT TOP (@MaxBatchSize) [SequenceNumber]
        FROM [dbo].[Events]
        WHERE [SequenceNumber] > @After
        ORDER BY [SequenceNumber]) AS [Next];

    IF @First IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[Batches] ([DispatcherId], [SequenceNumber], [LastSequenceNumber])
        VALUES (@DispatcherId, @First, @Last);

        SET @BatchId = SCOPE_IDENTITY();
    END

COMMIT TRANSACTION;

SELECT @BatchId AS [BatchId]
WHERE @BatchId IS NOT NULL;

SELECT [SequenceNumber], [TypeId], [Payload]
FROM [dbo].[Events]
WHERE @BatchId IS NOT NULL
    AND [SequenceNumber] BETWEEN @First AND @Last
ORDER BY [SequenceNumber];
GO

CREATE OR ALTER PROCEDURE [dbo].[MarkDispatched]
    @DispatcherId UNIQUEIDENTIFIER,
    @SequenceNumber BIGINT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

MERGE INTO [dbo].[DispatchedEvents] WITH (HOLDLOCK) AS [Target]
USING (SELECT @DispatcherId AS [DispatcherId], @SequenceNumber AS [SequenceNumber]) AS [Source]
ON [Target].[DispatcherId] = [Source].[DispatcherId]
WHEN MATCHED AND [Target].[SequenceNumber] < [Source].[SequenceNumber] THEN
    UPDATE SET [Target].[SequenceNumber] = [Source].[SequenceNumber]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([DispatcherId], [SequenceNumber])
    VALUES ([Source].[DispatcherId], [Source].[SequenceNumber]);

UPDATE [dbo].[Batches]
SET [Complete] = 1
WHERE [DispatcherId] = @DispatcherId
    AND [Complete] = 0
    AND [LastSequenceNumber] <= @SequenceNumber;
GO
