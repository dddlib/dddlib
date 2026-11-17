-- dddlib SQL Server schema, version 6: several aggregate roots saved in one transaction.
--
-- Applied by SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer), SqlServerEventDispatcherSchema.EnsureAsync
-- (dddlib.Persistence.EventDispatcher.SqlServer) or SqlServerProjectionsSchema.EnsureAsync
-- (dddlib.Persistence.Projections.SqlServer). Additions only: CommitStream, SaveMemento and AppendEvents keep their
-- contracts and bodies. Idempotent like the earlier versions.
--
-- Lock order. Every write that commits to several streams takes the application lock of each stream, in identity
-- order, before anything else, and the store-wide 'dddlib.Events.Commit' lock once, last, around inserting the events.
-- CommitStream takes its one stream's lock before the store-wide lock too, so single and multi-stream commits cannot
-- deadlock each other. Calling CommitStream once per stream inside one outer transaction would: after the first call
-- the transaction holds the store-wide lock and asks for the next stream's lock, which a single commit of that stream
-- holds while it waits for the store-wide lock.

-- Event store.

IF TYPE_ID('[dbo].[StreamCommitList]') IS NULL
CREATE TYPE [dbo].[StreamCommitList] AS TABLE
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [Order] INT NOT NULL,
    [PreCommitState] VARCHAR(36) NULL,
    PRIMARY KEY CLUSTERED ([StreamId] ASC)
);
GO

IF TYPE_ID('[dbo].[StreamEventList]') IS NULL
CREATE TYPE [dbo].[StreamEventList] AS TABLE
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [Index] INT NOT NULL,
    [TypeId] INT NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    PRIMARY KEY CLUSTERED ([StreamId] ASC, [Index] ASC)
);
GO

-- Commits the events of several streams, all or nothing. A stream whose state is not its commit's @PreCommitState
-- (null for a stream that must not exist yet) is reported in @ConflictStreamId, the first in commit order, and nothing
-- is written; no error is raised, so that the caller can name the stream. Otherwise returns each stream's new state
-- token in commit order. Sequence numbers follow the commit order and then each stream's event order.
CREATE OR ALTER PROCEDURE [dbo].[CommitStreams]
    @Streams [dbo].[StreamCommitList] READONLY,
    @Events [dbo].[StreamEventList] READONLY,
    @Metadata NVARCHAR(MAX),
    @CorrelationId UNIQUEIDENTIFIER,
    @ConflictStreamId UNIQUEIDENTIFIER = NULL OUTPUT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

SET @ConflictStreamId = NULL;

DECLARE @Commits TABLE
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Order] INT NOT NULL,
    [IsNew] BIT NOT NULL,
    [Revision] INT NOT NULL,
    [EventCount] INT NOT NULL,
    [PostCommitState] VARCHAR(36) NOT NULL
);

BEGIN TRANSACTION;

    DECLARE @Lock INT;
    DECLARE @StreamId UNIQUEIDENTIFIER;

    DECLARE [StreamIds] CURSOR LOCAL FAST_FORWARD FOR
        SELECT [StreamId] FROM @Streams ORDER BY [StreamId];

    OPEN [StreamIds];
    FETCH NEXT FROM [StreamIds] INTO @StreamId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC @Lock = sp_getapplock @Resource = @StreamId, @LockMode = 'Exclusive', @LockTimeout = 1000;
        IF @Lock < 0
            THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for stream.', 1;

        FETCH NEXT FROM [StreamIds] INTO @StreamId;
    END;

    CLOSE [StreamIds];
    DEALLOCATE [StreamIds];

    -- Under the stream locks no other writer can change these states.
    SELECT TOP (1) @ConflictStreamId = [Commit].[StreamId]
    FROM @Streams [Commit]
    LEFT JOIN [dbo].[Streams] [Stream] ON [Stream].[Id] = [Commit].[StreamId]
    WHERE ([Commit].[PreCommitState] IS NULL AND [Stream].[Id] IS NOT NULL)
        OR ([Commit].[PreCommitState] IS NOT NULL AND ([Stream].[Id] IS NULL OR [Stream].[State] <> [Commit].[PreCommitState]))
    ORDER BY [Commit].[Order];

    -- Nothing has been written: committing releases the locks and, unlike a rollback, leaves a caller's transaction
    -- intact.
    IF @ConflictStreamId IS NOT NULL
    BEGIN
        COMMIT TRANSACTION;
        RETURN;
    END;

    -- As in CommitStream: sequence numbers are assigned in commit order for the event dispatcher and the feed.
    EXEC @Lock = sp_getapplock @Resource = 'dddlib.Events.Commit', @LockMode = 'Exclusive', @LockTimeout = 10000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire the event sequence lock.', 1;

    INSERT INTO @Commits ([StreamId], [Order], [IsNew], [Revision], [EventCount], [PostCommitState])
    SELECT
        [Commit].[StreamId],
        [Commit].[Order],
        CASE WHEN [Stream].[Id] IS NULL THEN 1 ELSE 0 END,
        COALESCE([Stream].[Revision], 0),
        (SELECT COUNT(*) FROM @Events [Event] WHERE [Event].[StreamId] = [Commit].[StreamId]),
        LEFT(NEWID(), 8)
    FROM @Streams [Commit]
    LEFT JOIN [dbo].[Streams] [Stream] ON [Stream].[Id] = [Commit].[StreamId];

    UPDATE [Stream]
    SET [Stream].[Revision] = [Stream].[Revision] + [Commit].[EventCount], [Stream].[State] = [Commit].[PostCommitState]
    FROM [dbo].[Streams] [Stream]
    INNER JOIN @Commits [Commit] ON [Commit].[StreamId] = [Stream].[Id]
    WHERE [Commit].[IsNew] = 0;

    INSERT INTO [dbo].[Streams] ([Id], [Revision], [State])
    SELECT [StreamId], [EventCount], [PostCommitState]
    FROM @Commits
    WHERE [IsNew] = 1;

    INSERT INTO [dbo].[Events] ([StreamLinkId], [StreamRevision], [TypeId], [Metadata], [Payload], [CorrelationId], [SequenceNumber])
    SELECT
        [Stream].[LinkId],
        [Commit].[Revision] + ROW_NUMBER() OVER (PARTITION BY [Event].[StreamId] ORDER BY [Event].[Index]),
        [Event].[TypeId],
        @Metadata,
        [Event].[Payload],
        @CorrelationId,
        NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Commit].[Order] ASC, [Event].[Index] ASC)
    FROM @Events [Event]
    INNER JOIN @Commits [Commit] ON [Commit].[StreamId] = [Event].[StreamId]
    INNER JOIN [dbo].[Streams] [Stream] ON [Stream].[Id] = [Event].[StreamId]
    ORDER BY [Commit].[Order], [Event].[Index];

COMMIT TRANSACTION;

SELECT [StreamId], [PostCommitState]
FROM @Commits
ORDER BY [Order];
GO

-- Memento batches. A batch runs in one transaction that the client owns: LockStreams takes every identity's lock, then
-- SaveMementos writes the mementos in the Mementos table, custom storage writes its own tables, and AppendStreams
-- appends every aggregate root's events under the store-wide lock, last. Single memento saves take their identity's
-- lock first too (SaveMemento, and custom storage through the same path), so batches and single saves keep one order.

IF TYPE_ID('[dbo].[StreamIdList]') IS NULL
CREATE TYPE [dbo].[StreamIdList] AS TABLE
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY CLUSTERED ([StreamId] ASC)
);
GO

IF TYPE_ID('[dbo].[MementoList]') IS NULL
CREATE TYPE [dbo].[MementoList] AS TABLE
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Order] INT NOT NULL,
    [TypeId] INT NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    [PreCommitState] VARCHAR(36) NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

IF TYPE_ID('[dbo].[StreamAppendList]') IS NULL
CREATE TYPE [dbo].[StreamAppendList] AS TABLE
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [Order] INT NOT NULL,
    [State] VARCHAR(36) NOT NULL,
    [Metadata] NVARCHAR(MAX) NULL,
    PRIMARY KEY CLUSTERED ([StreamId] ASC)
);
GO

-- Takes the application lock of each identity, in identity order, owned by the caller's transaction, so that they are
-- held until it ends.
CREATE OR ALTER PROCEDURE [dbo].[LockStreams]
    @StreamIds [dbo].[StreamIdList] READONLY
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF @@TRANCOUNT = 0
    THROW 50000, 'LockStreams must run inside a transaction, which holds the locks until it ends.', 1;

DECLARE @Lock INT;
DECLARE @StreamId UNIQUEIDENTIFIER;

DECLARE [StreamIds] CURSOR LOCAL FAST_FORWARD FOR
    SELECT [StreamId] FROM @StreamIds ORDER BY [StreamId];

OPEN [StreamIds];
FETCH NEXT FROM [StreamIds] INTO @StreamId;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC @Lock = sp_getapplock @Resource = @StreamId, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for memento.', 1;

    FETCH NEXT FROM [StreamIds] INTO @StreamId;
END;

CLOSE [StreamIds];
DEALLOCATE [StreamIds];
GO

-- SaveMemento for several mementos, without their events, inside the caller's transaction and under its identity locks
-- (LockStreams). A memento whose state is not its @PreCommitState (null for a memento that must not exist yet) is
-- reported in @ConflictId, the first in order, and nothing is written; no error is raised, so that the caller can name
-- the aggregate root. Otherwise returns each memento's new state token in order.
CREATE OR ALTER PROCEDURE [dbo].[SaveMementos]
    @Mementos [dbo].[MementoList] READONLY,
    @ConflictId UNIQUEIDENTIFIER = NULL OUTPUT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF @@TRANCOUNT = 0
    THROW 50000, 'SaveMementos must run inside a transaction that holds the identity locks (LockStreams).', 1;

SET @ConflictId = NULL;

DECLARE @Saved TABLE
(
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Order] INT NOT NULL,
    [IsNew] BIT NOT NULL,
    [PostCommitState] VARCHAR(36) NOT NULL
);

SELECT TOP (1) @ConflictId = [Source].[Id]
FROM @Mementos [Source]
LEFT JOIN [dbo].[Mementos] [Target] ON [Target].[Id] = [Source].[Id]
WHERE ([Source].[PreCommitState] IS NULL AND [Target].[Id] IS NOT NULL)
    OR ([Source].[PreCommitState] IS NOT NULL AND ([Target].[Id] IS NULL OR [Target].[State] <> [Source].[PreCommitState]))
ORDER BY [Source].[Order];

IF @ConflictId IS NOT NULL
    RETURN;

INSERT INTO @Saved ([Id], [Order], [IsNew], [PostCommitState])
SELECT [Source].[Id], [Source].[Order], CASE WHEN [Target].[Id] IS NULL THEN 1 ELSE 0 END, LEFT(NEWID(), 8)
FROM @Mementos [Source]
LEFT JOIN [dbo].[Mementos] [Target] ON [Target].[Id] = [Source].[Id];

UPDATE [Target]
SET [Target].[TypeId] = [Source].[TypeId], [Target].[Payload] = [Source].[Payload], [Target].[State] = [Saved].[PostCommitState]
FROM [dbo].[Mementos] [Target]
INNER JOIN @Mementos [Source] ON [Source].[Id] = [Target].[Id]
INNER JOIN @Saved [Saved] ON [Saved].[Id] = [Target].[Id]
WHERE [Saved].[IsNew] = 0;

INSERT INTO [dbo].[Mementos] ([Id], [TypeId], [Payload], [State])
SELECT [Source].[Id], [Source].[TypeId], [Source].[Payload], [Saved].[PostCommitState]
FROM @Mementos [Source]
INNER JOIN @Saved [Saved] ON [Saved].[Id] = [Source].[Id]
WHERE [Saved].[IsNew] = 1;

SELECT [Id], [PostCommitState]
FROM @Saved
ORDER BY [Order];
GO

-- AppendEvents for several streams, inside the caller's transaction and under its identity locks (LockStreams). As in
-- AppendEvents the memento's state token is authoritative: each stream takes its @State without a check. The
-- store-wide lock is taken once, so the events get contiguous sequence numbers in stream order and then event order.
CREATE OR ALTER PROCEDURE [dbo].[AppendStreams]
    @Streams [dbo].[StreamAppendList] READONLY,
    @Events [dbo].[StreamEventList] READONLY,
    @CorrelationId UNIQUEIDENTIFIER
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

IF @@TRANCOUNT = 0
    THROW 50000, 'AppendStreams must run inside a transaction that holds the identity locks (LockStreams).', 1;

IF NOT EXISTS (SELECT 1 FROM @Events)
    RETURN;

DECLARE @Lock INT;

EXEC @Lock = sp_getapplock @Resource = 'dddlib.Events.Commit', @LockMode = 'Exclusive', @LockTimeout = 10000;
IF @Lock < 0
    THROW 50500, 'Concurrency error (server side). Failed to acquire the event sequence lock.', 1;

DECLARE @Appends TABLE
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Order] INT NOT NULL,
    [IsNew] BIT NOT NULL,
    [Revision] INT NOT NULL,
    [EventCount] INT NOT NULL,
    [State] VARCHAR(36) NOT NULL,
    [Metadata] NVARCHAR(MAX) NULL
);

INSERT INTO @Appends ([StreamId], [Order], [IsNew], [Revision], [EventCount], [State], [Metadata])
SELECT
    [Append].[StreamId],
    [Append].[Order],
    CASE WHEN [Stream].[Id] IS NULL THEN 1 ELSE 0 END,
    COALESCE([Stream].[Revision], 0),
    (SELECT COUNT(*) FROM @Events [Event] WHERE [Event].[StreamId] = [Append].[StreamId]),
    [Append].[State],
    [Append].[Metadata]
FROM @Streams [Append]
LEFT JOIN [dbo].[Streams] [Stream] ON [Stream].[Id] = [Append].[StreamId]
WHERE EXISTS (SELECT 1 FROM @Events [Event] WHERE [Event].[StreamId] = [Append].[StreamId]);

UPDATE [Stream]
SET [Stream].[Revision] = [Stream].[Revision] + [Append].[EventCount], [Stream].[State] = [Append].[State]
FROM [dbo].[Streams] [Stream]
INNER JOIN @Appends [Append] ON [Append].[StreamId] = [Stream].[Id]
WHERE [Append].[IsNew] = 0;

INSERT INTO [dbo].[Streams] ([Id], [Revision], [State])
SELECT [StreamId], [EventCount], [State]
FROM @Appends
WHERE [IsNew] = 1;

INSERT INTO [dbo].[Events] ([StreamLinkId], [StreamRevision], [TypeId], [Metadata], [Payload], [CorrelationId], [SequenceNumber])
SELECT
    [Stream].[LinkId],
    [Append].[Revision] + ROW_NUMBER() OVER (PARTITION BY [Event].[StreamId] ORDER BY [Event].[Index]),
    [Event].[TypeId],
    [Append].[Metadata],
    [Event].[Payload],
    @CorrelationId,
    NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Append].[Order] ASC, [Event].[Index] ASC)
FROM @Events [Event]
INNER JOIN @Appends [Append] ON [Append].[StreamId] = [Event].[StreamId]
INNER JOIN [dbo].[Streams] [Stream] ON [Stream].[Id] = [Event].[StreamId]
ORDER BY [Append].[Order], [Event].[Index];
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Versions] WHERE [Version] = 6)
INSERT INTO [dbo].[Versions] ([Version]) VALUES (6);
GO
