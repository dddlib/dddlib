-- dddlib SQL Server schema, version 4: the event dispatcher reads each event's metadata.
--
-- Applied by SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer), SqlServerEventDispatcherSchema.EnsureAsync
-- (dddlib.Persistence.EventDispatcher.SqlServer) or SqlServerProjectionsSchema.EnsureAsync
-- (dddlib.Persistence.Projections.SqlServer). GetNextBatch returns the events' Metadata as a fourth column, after
-- the three that dispatchers requiring version 1 to 3 read by position; no table changes. Idempotent like the
-- earlier versions.
--
-- Metadata holds dddlib's hostname and timestamp and, since this version, any entries a repository appends with the
-- events (https://github.com/dddlib/dddlib/issues/52).

-- Event dispatcher.

-- As in version 1, with the events' metadata.
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

SELECT [Event].[SequenceNumber], [Type].[Name] AS [TypeName], [Event].[Payload], [Event].[Metadata]
FROM [dbo].[Events] [Event] INNER JOIN [dbo].[Types] [Type] ON [Event].[TypeId] = [Type].[Id]
WHERE @BatchId IS NOT NULL
    AND [Event].[SequenceNumber] BETWEEN @First AND @Last
ORDER BY [Event].[SequenceNumber];
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Versions] WHERE [Version] = 4)
INSERT INTO [dbo].[Versions] ([Version]) VALUES (4);
GO
