-- dddlib SQL Server schema, version 9: every write of a type's natural keys takes the type's turn, and gives up clearly.
--
-- Applied by SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer), SqlServerEventDispatcherSchema.EnsureAsync
-- (dddlib.Persistence.EventDispatcher.SqlServer) or SqlServerProjectionsSchema.EnsureAsync
-- (dddlib.Persistence.Projections.SqlServer). Procedure bodies only, with the contracts of version 1 and 3. Idempotent
-- like the earlier versions (https://github.com/dddlib/dddlib/issues/57).
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

IF NOT EXISTS (SELECT * FROM [dbo].[Versions] WHERE [Version] = 9)
INSERT INTO [dbo].[Versions] ([Version]) VALUES (9);
GO
