-- dddlib SQL Server schema, version 5: concurrent first registrations of different types no longer deadlock, and
-- concurrent writers of a type's natural keys take turns.
--
-- Applied by SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer), SqlServerEventDispatcherSchema.EnsureAsync
-- (dddlib.Persistence.EventDispatcher.SqlServer) or SqlServerProjectionsSchema.EnsureAsync
-- (dddlib.Persistence.Projections.SqlServer). Procedure bodies only, with the contracts of version 1 and 3; no table
-- changes. Idempotent like the earlier versions.
--
-- Types were registered with MERGE ... WITH (HOLDLOCK). Its comparison is in a case-sensitive collation the [Name]
-- column does not have, so it scans the index, and under HOLDLOCK the scan takes shared range locks over all of it.
-- Two callers registering different types then each wait to convert their range lock to insert: a deadlock, and on a
-- freshly created database, where many types are new at once, a likely one
-- (https://github.com/dddlib/dddlib/issues/55). A type is now inserted without range locks, and a caller that loses
-- the race to insert the same name treats the duplicate key as success.
--
-- Every add and removal of a natural key writes at the next checkpoint of its type. Concurrent writers of a type read
-- the same latest checkpoint, collide on the primary key and retried at once, with no order among them: under 50
-- writers a 1000-operation run saw thousands of collisions, and the unluckiest removals took 10 to 20 seconds.
-- GetOrAddNaturalKey and RemoveNaturalKey now hold an exclusive application lock for the type while they take the
-- checkpoint, so writers wait in turn. They still retry a collision, which TryAddNaturalKey, which does not take the
-- lock, can cause.

-- Type registry.

-- As in version 1, without range locks. XACT_ABORT is off so that a duplicate key caught here leaves a caller's
-- transaction committable.
CREATE OR ALTER PROCEDURE [dbo].[TryAddType]
    @Name VARCHAR(511)
AS
SET NOCOUNT ON;
SET XACT_ABORT OFF;
SET ARITHABORT ON;

IF EXISTS (SELECT * FROM [dbo].[Types] WHERE [Name] = @Name COLLATE SQL_Latin1_General_CP1_CS_AS)
    RETURN;

BEGIN TRY
    INSERT INTO [dbo].[Types] ([Name])
    VALUES (@Name);
END TRY
BEGIN CATCH
    -- Another caller inserted it first.
    IF ERROR_NUMBER() NOT IN (2601, 2627)
        THROW;
END CATCH
GO

-- Identity map.

-- As in version 3, registering the type with TryAddType and adding the key under the type's checkpoint lock. A key
-- that already exists is found without the lock.
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

-- the hash finds the key; the value itself is compared too, so that a hash collision cannot match another key
INSERT INTO @NaturalKeys ([Id], [Checkpoint])
SELECT [Id], [Checkpoint]
FROM [dbo].[NaturalKeys]
WHERE [TypeId] = @TypeId AND [ValueHash] = @ValueHash AND [IsRemoved] = 0 AND [SerializedValue] = @SerializedValue;

WHILE NOT EXISTS (SELECT * FROM @NaturalKeys)
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

            EXEC @Lock = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction';
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
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        -- a table variable keeps its rows through a rollback
        DELETE FROM @NaturalKeys;

        IF ERROR_NUMBER() NOT IN (1205, 2601, 2627) OR @Attempt >= 100
            THROW;

        SET @Attempt += 1;
    END CATCH
END

SELECT [Id], [Checkpoint]
FROM @NaturalKeys;
GO

-- As in version 3, moving the record to the next checkpoint under the type's checkpoint lock.
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

            EXEC @Lock = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction';
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

        IF ERROR_NUMBER() NOT IN (1205, 2627) OR @Attempt >= 100
            THROW;

        SET @Attempt += 1;
    END CATCH
END
GO

-- Snapshot store.

-- As in version 1, registering the type with TryAddType.
CREATE OR ALTER PROCEDURE [dbo].[PutSnapshot]
    @StreamId UNIQUEIDENTIFIER,
    @StreamRevision INT,
    @PayloadTypeName VARCHAR(511),
    @Payload NVARCHAR(MAX)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

EXEC [dbo].[TryAddType] @Name = @PayloadTypeName;

DECLARE @TypeId INT = (SELECT [Id] FROM [dbo].[Types] WHERE [Name] = @PayloadTypeName COLLATE SQL_Latin1_General_CP1_CS_AS);

MERGE [dbo].[Snapshots] WITH (HOLDLOCK) AS [Target]
USING (SELECT @StreamId AS [StreamId], @StreamRevision AS [StreamRevision], @Payload AS [Payload]) AS [Source]
ON [Target].[StreamId] = [Source].[StreamId]
WHEN MATCHED THEN
    UPDATE SET
        [Target].[StreamRevision] = [Source].[StreamRevision],
        [Target].[TypeId] = @TypeId,
        [Target].[Payload] = [Source].[Payload]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([StreamId], [StreamRevision], [TypeId], [Payload])
    VALUES ([Source].[StreamId], [Source].[StreamRevision], @TypeId, [Source].[Payload]);
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Versions] WHERE [Version] = 5)
INSERT INTO [dbo].[Versions] ([Version]) VALUES (5);
GO
