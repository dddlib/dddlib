-- dddlib SQL Server schema, version 10: the event dispatcher completes only the batch a mark is for, and never moves
-- past a batch that is still open.
--
-- Applied by SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer), SqlServerEventDispatcherSchema.EnsureAsync
-- (dddlib.Persistence.EventDispatcher.SqlServer) or SqlServerProjectionsSchema.EnsureAsync
-- (dddlib.Persistence.Projections.SqlServer). Batches gains a nullable column and an index, and GetNextBatch and
-- MarkDispatched change bodies, with the contracts of version 4 and 1. Idempotent like the earlier versions
-- (https://github.com/dddlib/dddlib/issues/53).
--
-- MarkDispatched completed every open batch of the dispatcher that ended at or before the sequence number it was given,
-- and moved the position there. A batch that another host with the same dispatcher id had taken and not finished, or one
-- a host stopped partway through before it restarted, was completed by the next mark of a later batch, and its remaining
-- events were never dispatched. A mark now records how far through its own batch the host has dispatched, and the
-- position moves only over the batches completed after it. Batches never overlap, so the open batch that holds the
-- sequence number is the caller's: processes on older packages, which mark the same way, get the fix as soon as this
-- version is applied.
--
-- A batch that timed out was marked complete and the next batch was read from the position, so it came back only when
-- no later batch was open. An open batch that times out is now handed out again itself, earliest first, with the events
-- after those dispatched from it. A new batch starts after every batch already handed out. Complete now means
-- dispatched, not abandoned.
--
-- Polls and marks for one dispatcher id take turns on a lock named after the schema's Batches table, so dispatchers in
-- two dddlib schemas with the same dispatcher id no longer wait for each other.

-- Event dispatcher.

-- The last event dispatched from the batch, while it is open, or its last event once it is complete. NULL for a batch
-- that nothing has been dispatched from.
IF COL_LENGTH(N'[dbo].[Batches]', N'DispatchedSequenceNumber') IS NULL
ALTER TABLE [dbo].[Batches] ADD [DispatchedSequenceNumber] BIGINT NULL;
GO

-- The batches past a dispatcher's position, in order. IX_Batch_Dispatcher is no longer used: no package refers to it by
-- name, and a later version may drop it.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Batches]') AND name = N'IX_Batch_Dispatcher_Last')
CREATE INDEX [IX_Batch_Dispatcher_Last] ON [dbo].[Batches] ([DispatcherId], [LastSequenceNumber])
    INCLUDE ([SequenceNumber], [DispatchedSequenceNumber], [Timestamp], [Complete]);
GO

-- A batch that timed out before this version was marked complete although its events after the position had not all
-- been dispatched, and the dispatcher would read them again from the position. It is removed, so that it does not count
-- as handed out. A batch completed by a mark ends at or before the position and stays as it is; one completed from this
-- version on has DispatchedSequenceNumber set, so running the script again removes nothing.
DELETE [Batch]
FROM [dbo].[Batches] [Batch]
LEFT JOIN [dbo].[DispatchedEvents] [Dispatched] ON [Dispatched].[DispatcherId] = [Batch].[DispatcherId]
WHERE [Batch].[Complete] = 1
    AND [Batch].[DispatchedSequenceNumber] IS NULL
    AND [Batch].[LastSequenceNumber] > COALESCE([Dispatched].[SequenceNumber], 0);
GO

-- As in version 4: hands out again the earliest open batch that has timed out, or else a new batch after every batch
-- already handed out.
CREATE OR ALTER PROCEDURE [dbo].[GetNextBatch]
    @DispatcherId UNIQUEIDENTIFIER,
    @MaxBatchSize INT,
    @BatchTimeoutMilliseconds INT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Lock INT;
DECLARE @Position BIGINT;
DECLARE @After BIGINT;
DECLARE @First BIGINT;
DECLARE @Last BIGINT;
DECLARE @BatchId BIGINT;

BEGIN TRANSACTION;

    DECLARE @Resource NVARCHAR(255) = CONCAT(N'dddlib.Batches.', OBJECT_ID(N'[dbo].[Batches]'), N'.', @DispatcherId);
    EXEC @Lock = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire the batch lock for the dispatcher.', 1;

    SET @Position = COALESCE((SELECT [SequenceNumber] FROM [dbo].[DispatchedEvents] WHERE [DispatcherId] = @DispatcherId), 0);

    -- An open batch that has timed out comes back with the events after the position and after those dispatched from
    -- it, up to its own last event, however many that is. The comparison is at millisecond precision: DATEDIFF(SECOND)
    -- counts second boundaries crossed, which would expire a batch taken at xx.999 at (xx+1).001.
    SELECT TOP (1)
        @BatchId = [Id],
        @After = (SELECT MAX([Value]) FROM (VALUES (@Position), ([SequenceNumber] - 1), ([DispatchedSequenceNumber])) AS [Positions] ([Value])),
        @Last = [LastSequenceNumber]
    FROM [dbo].[Batches]
    WHERE [DispatcherId] = @DispatcherId
        AND [LastSequenceNumber] > @Position
        AND [Complete] = 0
        AND [Timestamp] <= DATEADD(MILLISECOND, -@BatchTimeoutMilliseconds, SYSUTCDATETIME())
    ORDER BY [LastSequenceNumber];

    IF @BatchId IS NOT NULL
    BEGIN
        UPDATE [dbo].[Batches]
        SET [Timestamp] = SYSUTCDATETIME()
        WHERE [Id] = @BatchId;
    END
    ELSE
    BEGIN
        -- A new batch starts after the batches still open and those completed past the position, so batches never
        -- overlap.
        SET @After = COALESCE(
            (SELECT MAX([LastSequenceNumber]) FROM [dbo].[Batches] WHERE [DispatcherId] = @DispatcherId AND [LastSequenceNumber] > @Position),
            @Position);

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
    END

COMMIT TRANSACTION;

SELECT @BatchId AS [BatchId]
WHERE @BatchId IS NOT NULL;

SELECT [Event].[SequenceNumber], [Type].[Name] AS [TypeName], [Event].[Payload], [Event].[Metadata]
FROM [dbo].[Events] [Event] INNER JOIN [dbo].[Types] [Type] ON [Event].[TypeId] = [Type].[Id]
WHERE @BatchId IS NOT NULL
    AND [Event].[SequenceNumber] > @After
    AND [Event].[SequenceNumber] <= @Last
ORDER BY [Event].[SequenceNumber];
GO

-- As in version 1: records how far through the caller's batch the dispatcher has got, completes that batch only, and
-- moves the position over the batches completed after it.
CREATE OR ALTER PROCEDURE [dbo].[MarkDispatched]
    @DispatcherId UNIQUEIDENTIFIER,
    @SequenceNumber BIGINT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Lock INT;
DECLARE @Position BIGINT;
DECLARE @Open BIGINT;
DECLARE @OpenDispatched BIGINT;
DECLARE @Completed BIGINT;

BEGIN TRANSACTION;

    -- The poll's lock: two marks completing adjacent batches at once would each see the other's batch open, and leave
    -- the position short of both.
    DECLARE @Resource NVARCHAR(255) = CONCAT(N'dddlib.Batches.', OBJECT_ID(N'[dbo].[Batches]'), N'.', @DispatcherId);
    EXEC @Lock = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockTimeout = 10000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire the batch lock for the dispatcher.', 1;

    SET @Position = COALESCE((SELECT [SequenceNumber] FROM [dbo].[DispatchedEvents] WHERE [DispatcherId] = @DispatcherId), 0);

    -- Batches never overlap, so the open batch that holds the sequence number is the caller's. A host that took the
    -- batch before it timed out may mark it after the host that took it again: either has dispatched the events up to
    -- the sequence number it marks, so the furthest mark stands.
    UPDATE [dbo].[Batches]
    SET [DispatchedSequenceNumber] = @SequenceNumber,
        [Complete] = CASE WHEN @SequenceNumber >= [LastSequenceNumber] THEN 1 ELSE 0 END
    WHERE [DispatcherId] = @DispatcherId
        AND [LastSequenceNumber] >= @SequenceNumber
        AND [SequenceNumber] <= @SequenceNumber
        AND [Complete] = 0
        AND ([DispatchedSequenceNumber] IS NULL OR [DispatchedSequenceNumber] < @SequenceNumber);

    -- The position moves over the completed batches that follow it, and into the first open batch as far as it has been
    -- dispatched, but never past that batch.
    SELECT TOP (1) @Open = [SequenceNumber], @OpenDispatched = [DispatchedSequenceNumber]
    FROM [dbo].[Batches]
    WHERE [DispatcherId] = @DispatcherId
        AND [LastSequenceNumber] > @Position
        AND [Complete] = 0
    ORDER BY [LastSequenceNumber];

    SET @Completed = (
        SELECT MAX([LastSequenceNumber])
        FROM [dbo].[Batches]
        WHERE [DispatcherId] = @DispatcherId
            AND [LastSequenceNumber] > @Position
            AND [Complete] = 1
            AND (@Open IS NULL OR [LastSequenceNumber] < @Open));

    SET @Position = (SELECT MAX([Value]) FROM (VALUES (@Position), (@Completed), (@OpenDispatched)) AS [Positions] ([Value]));

    MERGE INTO [dbo].[DispatchedEvents] WITH (HOLDLOCK) AS [Target]
    USING (SELECT @DispatcherId AS [DispatcherId], @Position AS [SequenceNumber]) AS [Source]
    ON [Target].[DispatcherId] = [Source].[DispatcherId]
    WHEN MATCHED AND [Target].[SequenceNumber] < [Source].[SequenceNumber] THEN
        UPDATE SET [Target].[SequenceNumber] = [Source].[SequenceNumber]
    WHEN NOT MATCHED BY TARGET AND [Source].[SequenceNumber] > 0 THEN
        INSERT ([DispatcherId], [SequenceNumber])
        VALUES ([Source].[DispatcherId], [Source].[SequenceNumber]);

COMMIT TRANSACTION;
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Versions] WHERE [Version] = 10)
INSERT INTO [dbo].[Versions] ([Version]) VALUES (10);
GO
