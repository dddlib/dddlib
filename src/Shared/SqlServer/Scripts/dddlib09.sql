-- dddlib SQL Server schema, version 9: every write of a type's natural keys takes the type's turn, gives up clearly,
-- and holds the turn for less time, and commits in one schema no longer wait for those in another.
--
-- Applied by SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer), SqlServerEventDispatcherSchema.EnsureAsync
-- (dddlib.Persistence.EventDispatcher.SqlServer) or SqlServerProjectionsSchema.EnsureAsync
-- (dddlib.Persistence.Projections.SqlServer). The NaturalKeys table is clustered differently, and procedure bodies
-- change, with the contracts of version 1 and 3. Idempotent like the earlier versions
-- (https://github.com/dddlib/dddlib/issues/57).
--
-- Re-clustering rebuilds the NaturalKeys table inside the upgrade's transaction, so the upgrade holds it for as long as
-- that takes.
--
-- Version 5 made GetOrAddNaturalKey and RemoveNaturalKey hold the type's checkpoint lock while they take its next
-- checkpoint. TryAddNaturalKey, for keys the identity map compares itself, did not, so it collided with itself and with
-- them on the primary key; it now takes the lock too, and its only failure is a stale checkpoint, which sends the caller
-- to synchronize and try again. With every writer taking turns, a collision cannot happen, so the procedures retry a
-- deadlock only. It also registers the type without range locks, as the others have since version 5.
--
-- The lock was waited for until the client's command timeout, which the caller saw as a generic timeout. It is now
-- waited for 10 seconds, as the store-wide commit lock is, and then fails with error 50500, which the repository
-- reports as a concurrency error.

-- Identity map.

-- The natural keys were clustered on (TypeId, Checkpoint). A removal moves its key to the type's next checkpoint, so it
-- moved the whole row, serialized value and all, while it held the type's lock, and that lock is what sets how many
-- writes a type takes a second. They are now clustered on their identity, which never changes, and PK_NaturalKey
-- becomes a narrow nonclustered index on the same columns: a removal updates that index and the row in place. The
-- primary key keeps its name and columns, so a duplicate checkpoint is still error 2627 on PK_NaturalKey. Older
-- packages use none of the index names. IX_NaturalKey_Id, which version 3 added for removals to find their key, is
-- the clustered index now.
IF EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NaturalKeys]') AND name = N'IX_NaturalKey_Id')
DROP INDEX [IX_NaturalKey_Id] ON [dbo].[NaturalKeys];
GO

IF EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NaturalKeys]') AND name = N'PK_NaturalKey' AND type_desc = N'CLUSTERED')
ALTER TABLE [dbo].[NaturalKeys] DROP CONSTRAINT [PK_NaturalKey];
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NaturalKeys]') AND name = N'CX_NaturalKey_Id')
CREATE UNIQUE CLUSTERED INDEX [CX_NaturalKey_Id] ON [dbo].[NaturalKeys] ([Id]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NaturalKeys]') AND name = N'PK_NaturalKey')
ALTER TABLE [dbo].[NaturalKeys] ADD CONSTRAINT [PK_NaturalKey] PRIMARY KEY NONCLUSTERED ([TypeId], [Checkpoint]);
GO

-- As in version 1, in two statements. Reading the serialized values through the narrow index in one statement would
-- hold a key of that index while waiting for the row, and a removal locks the row and then that key: a deadlock. The
-- keys after the checkpoint are read from the index first, and then each row by its identity. A key removed in between
-- has moved to a later checkpoint; it is left out, and the next read finds it there.
CREATE OR ALTER PROCEDURE [dbo].[GetNaturalKeys]
    @AggregateRootTypeName VARCHAR(511),
    @Checkpoint BIGINT
AS
SET NOCOUNT ON;

DECLARE @Keys TABLE ([Checkpoint] BIGINT NOT NULL PRIMARY KEY, [Id] UNIQUEIDENTIFIER NOT NULL);

INSERT INTO @Keys ([Id], [Checkpoint])
SELECT [NaturalKey].[Id], [NaturalKey].[Checkpoint]
FROM [dbo].[NaturalKeys] [NaturalKey] INNER JOIN [dbo].[Types] [Type] ON [NaturalKey].[TypeId] = [Type].[Id]
WHERE [NaturalKey].[Checkpoint] > @Checkpoint AND [Type].[Name] = @AggregateRootTypeName;

SELECT [NaturalKey].[Id], [NaturalKey].[SerializedValue], [NaturalKey].[Checkpoint], [NaturalKey].[IsRemoved]
FROM @Keys [Key] INNER JOIN [dbo].[NaturalKeys] [NaturalKey] ON [NaturalKey].[Id] = [Key].[Id] AND [NaturalKey].[Checkpoint] = [Key].[Checkpoint]
ORDER BY [NaturalKey].[Checkpoint];
GO

-- As in version 1, adding the key under the type's checkpoint lock, and registering the type with TryAddType.
CREATE OR ALTER PROCEDURE [dbo].[TryAddNaturalKey]
    @AggregateRootTypeName VARCHAR(511),
    @SerializedValue NVARCHAR(MAX),
    @Checkpoint BIGINT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TypeId INT = (SELECT [Id] FROM [dbo].[Types] WHERE [Name] = @AggregateRootTypeName COLLATE SQL_Latin1_General_CP1_CS_AS);

IF @TypeId IS NULL
BEGIN
    EXEC [dbo].[TryAddType] @Name = @AggregateRootTypeName;

    SET @TypeId = (SELECT [Id] FROM [dbo].[Types] WHERE [Name] = @AggregateRootTypeName COLLATE SQL_Latin1_General_CP1_CS_AS);
END

DECLARE @NaturalKeys TABLE ([Id] uniqueidentifier, [Checkpoint] bigint);
DECLARE @Resource NVARCHAR(255) = CONCAT(N'dddlib.NaturalKeys.', OBJECT_ID(N'[dbo].[NaturalKeys]'), N'.', @TypeId);
DECLARE @Lock INT;

-- The caller has compared its key with every key present at its checkpoint. A key added since, and not removed, may
-- equal it: the checkpoint is stale, and the caller must synchronize and try again. A removal since cannot make the
-- caller's key a duplicate, so it does not. A caller that is stale already is told so without waiting for the lock.
IF NOT EXISTS (SELECT * FROM [dbo].[NaturalKeys] WHERE [TypeId] = @TypeId AND [Checkpoint] > @Checkpoint AND [IsRemoved] = 0)
BEGIN
    BEGIN TRANSACTION;

        EXEC @Lock = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
        IF @Lock < 0
            THROW 50500, 'Concurrency error (server side). Failed to acquire the natural key checkpoint lock.', 1;

        IF NOT EXISTS (SELECT * FROM [dbo].[NaturalKeys] WHERE [TypeId] = @TypeId AND [Checkpoint] > @Checkpoint AND [IsRemoved] = 0)
        INSERT INTO [dbo].[NaturalKeys] ([TypeId], [Checkpoint], [SerializedValue])
        OUTPUT inserted.[Id], inserted.[Checkpoint] INTO @NaturalKeys
        SELECT @TypeId, COALESCE(MAX([Checkpoint]), 0) + CONVERT(BIGINT, 1), @SerializedValue
        FROM [dbo].[NaturalKeys]
        WHERE [TypeId] = @TypeId;

    COMMIT TRANSACTION;
END

SELECT [Id], [Checkpoint]
FROM @NaturalKeys;
GO

-- As in version 5, waiting for the type's checkpoint lock for 10 seconds at most and retrying a deadlock only.
CREATE OR ALTER PROCEDURE [dbo].[GetOrAddNaturalKey]
    @AggregateRootTypeName VARCHAR(511),
    @SerializedValue NVARCHAR(MAX)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TypeId INT = (SELECT [Id] FROM [dbo].[Types] WHERE [Name] = @AggregateRootTypeName COLLATE SQL_Latin1_General_CP1_CS_AS);

IF @TypeId IS NULL
BEGIN
    EXEC [dbo].[TryAddType] @Name = @AggregateRootTypeName;

    SET @TypeId = (SELECT [Id] FROM [dbo].[Types] WHERE [Name] = @AggregateRootTypeName COLLATE SQL_Latin1_General_CP1_CS_AS);
END

DECLARE @ValueHash BINARY(32) = CONVERT(BINARY(32), HASHBYTES('SHA2_256', @SerializedValue));
DECLARE @NaturalKeys TABLE ([Id] uniqueidentifier, [Checkpoint] bigint);
DECLARE @Resource NVARCHAR(255) = CONCAT(N'dddlib.NaturalKeys.', OBJECT_ID(N'[dbo].[NaturalKeys]'), N'.', @TypeId);
DECLARE @Lock INT;
DECLARE @Attempt INT = 1;

WHILE NOT EXISTS (SELECT * FROM @NaturalKeys)
BEGIN
    BEGIN TRY
        -- A key that is present is found without the lock. The hash finds the key; the value itself is compared too, so
        -- that a hash collision cannot match another key.
        INSERT INTO @NaturalKeys ([Id], [Checkpoint])
        SELECT [Id], [Checkpoint]
        FROM [dbo].[NaturalKeys]
        WHERE [TypeId] = @TypeId AND [ValueHash] = @ValueHash AND [IsRemoved] = 0 AND [SerializedValue] = @SerializedValue;

        IF NOT EXISTS (SELECT * FROM @NaturalKeys)
        BEGIN
            BEGIN TRANSACTION;

                EXEC @Lock = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @Lock < 0
                    THROW 50500, 'Concurrency error (server side). Failed to acquire the natural key checkpoint lock.', 1;

                -- another writer may have added it while this one waited
                INSERT INTO @NaturalKeys ([Id], [Checkpoint])
                SELECT [Id], [Checkpoint]
                FROM [dbo].[NaturalKeys]
                WHERE [TypeId] = @TypeId AND [ValueHash] = @ValueHash AND [IsRemoved] = 0 AND [SerializedValue] = @SerializedValue;

                IF NOT EXISTS (SELECT * FROM @NaturalKeys)
                INSERT INTO [dbo].[NaturalKeys] ([TypeId], [Checkpoint], [SerializedValue])
                OUTPUT inserted.[Id], inserted.[Checkpoint] INTO @NaturalKeys
                SELECT @TypeId, COALESCE(MAX([Checkpoint]), 0) + CONVERT(BIGINT, 1), @SerializedValue
                FROM [dbo].[NaturalKeys]
                WHERE [TypeId] = @TypeId;

            COMMIT TRANSACTION;
        END
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        -- a table variable keeps its rows through a rollback
        DELETE FROM @NaturalKeys;

        IF ERROR_NUMBER() <> 1205 OR @Attempt >= 100
            THROW;

        SET @Attempt += 1;
    END CATCH
END

SELECT [Id], [Checkpoint]
FROM @NaturalKeys;
GO

-- As in version 5, waiting for the type's checkpoint lock for 10 seconds at most and retrying a deadlock only.
CREATE OR ALTER PROCEDURE [dbo].[RemoveNaturalKey]
    @Id UNIQUEIDENTIFIER
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TypeId INT = (SELECT [TypeId] FROM [dbo].[NaturalKeys] WHERE [Id] = @Id);
DECLARE @Resource NVARCHAR(255) = CONCAT(N'dddlib.NaturalKeys.', OBJECT_ID(N'[dbo].[NaturalKeys]'), N'.', @TypeId);
DECLARE @Lock INT;
DECLARE @Attempt INT = 1;

IF @TypeId IS NULL
    RETURN;

WHILE 1 = 1
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

            EXEC @Lock = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @Lock < 0
                THROW 50500, 'Concurrency error (server side). Failed to acquire the natural key checkpoint lock.', 1;

            UPDATE [dbo].[NaturalKeys]
            SET [IsRemoved] = 1, [Checkpoint] = (SELECT COALESCE(MAX([Checkpoint]), 0) + CONVERT(BIGINT, 1) FROM [dbo].[NaturalKeys] WHERE [TypeId] = @TypeId)
            WHERE [Id] = @Id AND [IsRemoved] = 0;

        COMMIT TRANSACTION;
        RETURN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        IF ERROR_NUMBER() <> 1205 OR @Attempt >= 100
            THROW;

        SET @Attempt += 1;
    END CATCH
END
GO

-- Event store.

-- The store-wide commit lock was named 'dddlib.Events.Commit' whatever the schema, so the commits of two dddlib schemas
-- in one database waited for each other. It is now named after the schema's Events table, as the natural key locks are
-- after its NaturalKeys table. While this version is applied, a commit already waiting for the old name can overlap
-- one taking the new name, once.

-- As in version 1, with the store-wide commit lock named after the schema.
CREATE OR ALTER PROCEDURE [dbo].[CommitStream]
    @StreamId UNIQUEIDENTIFIER,
    @Events [dbo].[EventList] READONLY,
    @Metadata NVARCHAR(MAX),
    @CorrelationId UNIQUEIDENTIFIER,
    @PreCommitState VARCHAR(36),
    @PostCommitState VARCHAR(36) = NULL OUTPUT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

BEGIN TRANSACTION;

    DECLARE @Lock INT;

    EXEC @Lock = sp_getapplock @Resource = @StreamId, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for stream.', 1;

    -- Commits are serialized so that sequence numbers are assigned in commit order: a dispatcher that has passed
    -- sequence number N can rely on every event below N being committed or permanently rolled back.
    DECLARE @CommitResource NVARCHAR(255) = CONCAT(N'dddlib.Events.Commit.', OBJECT_ID(N'[dbo].[Events]'));
    EXEC @Lock = sp_getapplock @Resource = @CommitResource, @LockMode = 'Exclusive', @LockTimeout = 10000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire the event sequence lock.', 1;

    DECLARE @StreamRevision INT = 0;
    DECLARE @Stream TABLE ([LinkId] BIGINT, [Revision] INT);

    SET @PostCommitState = LEFT(NEWID(), 8);

    MERGE INTO [dbo].[Streams] AS [Target]
    USING (
        SELECT @StreamId AS [Id], COUNT([Index]) AS [EventCount], @PreCommitState AS [State], @PostCommitState AS [NewState]
        FROM @Events) AS [Source]
    ON [Target].[Id] = [Source].[Id]
    WHEN MATCHED AND [Target].[State] = [Source].[State] THEN
        UPDATE SET [Target].[Revision] = [Target].[Revision] + [Source].[EventCount], [Target].[State] = [Source].[NewState], @StreamRevision = [Target].[Revision]
    WHEN NOT MATCHED BY TARGET AND [Source].[State] IS NULL THEN
        INSERT ([Id], [Revision], [State])
        VALUES ([Source].[Id], [Source].[EventCount], [Source].[NewState])
    OUTPUT inserted.[LinkId], inserted.[Revision] INTO @Stream;

    IF NOT EXISTS (SELECT 1 FROM @Stream)
        THROW 50409, 'Concurrency error (client side). Commit state mismatch.', 1;

    INSERT INTO [dbo].[Events] ([StreamLinkId], [StreamRevision], [TypeId], [Metadata], [Payload], [CorrelationId], [SequenceNumber])
    SELECT
        [Stream].[LinkId],
        @StreamRevision + ROW_NUMBER() OVER (ORDER BY [Event].[Index]),
        [Event].[TypeId],
        @Metadata,
        [Event].[Payload],
        @CorrelationId,
        NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Event].[Index] ASC)
    FROM @Events [Event] CROSS JOIN @Stream [Stream]
    ORDER BY [Event].[Index];

COMMIT TRANSACTION;
GO

-- As in version 6, with the store-wide commit lock named after the schema.
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
    DECLARE @CommitResource NVARCHAR(255) = CONCAT(N'dddlib.Events.Commit.', OBJECT_ID(N'[dbo].[Events]'));
    EXEC @Lock = sp_getapplock @Resource = @CommitResource, @LockMode = 'Exclusive', @LockTimeout = 10000;
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

-- As in version 7, with the store-wide commit lock named after the schema.
CREATE OR ALTER PROCEDURE [dbo].[AppendEvents]
    @StreamId UNIQUEIDENTIFIER,
    @Events [dbo].[EventList] READONLY,
    @State VARCHAR(36),
    @Metadata NVARCHAR(MAX) = NULL,
    @CorrelationId UNIQUEIDENTIFIER = NULL,
    @EventMetadata [dbo].[EventMetadataList] READONLY
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

IF NOT EXISTS (SELECT 1 FROM @Events)
    RETURN;

BEGIN TRANSACTION;

    DECLARE @Lock INT;

    EXEC @Lock = sp_getapplock @Resource = @StreamId, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for stream.', 1;

    -- As in CommitStream: sequence numbers are assigned in commit order for the event dispatcher.
    DECLARE @CommitResource NVARCHAR(255) = CONCAT(N'dddlib.Events.Commit.', OBJECT_ID(N'[dbo].[Events]'));
    EXEC @Lock = sp_getapplock @Resource = @CommitResource, @LockMode = 'Exclusive', @LockTimeout = 10000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire the event sequence lock.', 1;

    DECLARE @StreamRevision INT = 0;
    DECLARE @Correlation UNIQUEIDENTIFIER = COALESCE(@CorrelationId, NEWID());
    DECLARE @Stream TABLE ([LinkId] BIGINT, [Revision] INT);

    MERGE INTO [dbo].[Streams] AS [Target]
    USING (SELECT @StreamId AS [Id], COUNT([Index]) AS [EventCount] FROM @Events) AS [Source]
    ON [Target].[Id] = [Source].[Id]
    WHEN MATCHED THEN
        UPDATE SET [Target].[Revision] = [Target].[Revision] + [Source].[EventCount], [Target].[State] = @State, @StreamRevision = [Target].[Revision]
    WHEN NOT MATCHED BY TARGET THEN
        INSERT ([Id], [Revision], [State])
        VALUES ([Source].[Id], [Source].[EventCount], @State)
    OUTPUT inserted.[LinkId], inserted.[Revision] INTO @Stream;

    INSERT INTO [dbo].[Events] ([StreamLinkId], [StreamRevision], [TypeId], [Metadata], [Payload], [CorrelationId], [SequenceNumber])
    SELECT
        [Stream].[LinkId],
        @StreamRevision + ROW_NUMBER() OVER (ORDER BY [Event].[Index]),
        [Event].[TypeId],
        COALESCE([Own].[Metadata], @Metadata),
        [Event].[Payload],
        @Correlation,
        NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Event].[Index] ASC)
    FROM @Events [Event]
    CROSS JOIN @Stream [Stream]
    LEFT JOIN @EventMetadata [Own] ON [Own].[StreamId] = @StreamId AND [Own].[Index] = [Event].[Index]
    ORDER BY [Event].[Index];

COMMIT TRANSACTION;
GO

-- As in version 7, with the store-wide commit lock named after the schema.
CREATE OR ALTER PROCEDURE [dbo].[AppendStreams]
    @Streams [dbo].[StreamAppendList] READONLY,
    @Events [dbo].[StreamEventList] READONLY,
    @CorrelationId UNIQUEIDENTIFIER,
    @EventMetadata [dbo].[EventMetadataList] READONLY
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

IF @@TRANCOUNT = 0
    THROW 50000, 'AppendStreams must run inside a transaction that holds the identity locks (LockStreams).', 1;

IF NOT EXISTS (SELECT 1 FROM @Events)
    RETURN;

DECLARE @Lock INT;
DECLARE @CommitResource NVARCHAR(255) = CONCAT(N'dddlib.Events.Commit.', OBJECT_ID(N'[dbo].[Events]'));

EXEC @Lock = sp_getapplock @Resource = @CommitResource, @LockMode = 'Exclusive', @LockTimeout = 10000;
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
    COALESCE([Own].[Metadata], [Append].[Metadata]),
    [Event].[Payload],
    @CorrelationId,
    NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Append].[Order] ASC, [Event].[Index] ASC)
FROM @Events [Event]
INNER JOIN @Appends [Append] ON [Append].[StreamId] = [Event].[StreamId]
INNER JOIN [dbo].[Streams] [Stream] ON [Stream].[Id] = [Event].[StreamId]
LEFT JOIN @EventMetadata [Own] ON [Own].[StreamId] = [Event].[StreamId] AND [Own].[Index] = [Event].[Index]
ORDER BY [Append].[Order], [Event].[Index];
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Versions] WHERE [Version] = 9)
INSERT INTO [dbo].[Versions] ([Version]) VALUES (9);
GO
