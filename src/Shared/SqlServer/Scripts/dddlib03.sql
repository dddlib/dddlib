-- dddlib SQL Server schema, version 3: natural keys that writers of different keys do not contend for.
--
-- Applied by SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer), SqlServerEventDispatcherSchema.EnsureAsync
-- (dddlib.Persistence.EventDispatcher.SqlServer) or SqlServerProjectionsSchema.EnsureAsync
-- (dddlib.Persistence.Projections.SqlServer). Expand only: a column, two indexes and a procedure are added, and
-- RemoveNaturalKey keeps its parameters and effect, so packages that require version 1 or 2 keep working. Idempotent
-- like the earlier versions.
--
-- Every natural key of an aggregate root type shares the type's checkpoints. Adding a key used to require the caller
-- to have seen every key at the latest checkpoint, so any write to the type sent every other writer back to
-- synchronize and retry. GetOrAddNaturalKey instead finds a key that is present by its serialized value and otherwise
-- adds it at the next checkpoint, in one call, with a unique index keeping two writers of the same key from both
-- adding it. It is for keys whose equal values serialize alike; the identity map compares other keys itself.

-- Natural keys (identity map).

-- A hash of the serialized value to index, since NVARCHAR(MAX) cannot be.
IF COL_LENGTH('[dbo].[NaturalKeys]', 'ValueHash') IS NULL
ALTER TABLE [dbo].[NaturalKeys] ADD [ValueHash] AS CONVERT(BINARY(32), HASHBYTES('SHA2_256', [SerializedValue])) PERSISTED;
GO

-- One key that is present per serialized value, whichever procedure adds it.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NaturalKeys]') AND name = N'UX_NaturalKey_Value')
CREATE UNIQUE INDEX [UX_NaturalKey_Value] ON [dbo].[NaturalKeys] ([TypeId], [ValueHash]) WHERE [IsRemoved] = 0;
GO

-- A removal finds its key by identity; without this it scans, and locks, the whole table.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NaturalKeys]') AND name = N'IX_NaturalKey_Id')
CREATE INDEX [IX_NaturalKey_Id] ON [dbo].[NaturalKeys] ([Id]);
GO

-- The key that is present with the serialized value, or a new one at the next checkpoint. A writer that loses the race
-- for the checkpoint (a primary key violation) or for the key (a unique index violation), or is chosen as a deadlock
-- victim, tries again here, without a round trip; a key added by another writer is then found.
CREATE OR ALTER PROCEDURE [dbo].[GetOrAddNaturalKey]
    @AggregateRootTypeName VARCHAR(511),
    @SerializedValue NVARCHAR(MAX)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TypeId INT = (SELECT [Id] FROM [dbo].[Types] WHERE [Name] = @AggregateRootTypeName COLLATE SQL_Latin1_General_CP1_CS_AS);

-- Register the type the first time, atomically: two concurrent callers must not both insert it.
IF @TypeId IS NULL
BEGIN
    MERGE INTO [dbo].[Types] WITH (HOLDLOCK) AS [Target]
    USING (SELECT @AggregateRootTypeName AS [Name]) AS [Source]
    ON [Target].[Name] = [Source].[Name] COLLATE SQL_Latin1_General_CP1_CS_AS
    WHEN NOT MATCHED BY TARGET THEN
        INSERT ([Name])
        VALUES ([Source].[Name]);

    SET @TypeId = (SELECT [Id] FROM [dbo].[Types] WHERE [Name] = @AggregateRootTypeName COLLATE SQL_Latin1_General_CP1_CS_AS);
END

DECLARE @ValueHash BINARY(32) = CONVERT(BINARY(32), HASHBYTES('SHA2_256', @SerializedValue));
DECLARE @NaturalKeys TABLE ([Id] uniqueidentifier, [Checkpoint] bigint);
DECLARE @Attempt INT = 1;

WHILE NOT EXISTS (SELECT * FROM @NaturalKeys)
BEGIN
    -- the hash finds the key; the value itself is compared too, so that a hash collision cannot match another key
    INSERT INTO @NaturalKeys ([Id], [Checkpoint])
    SELECT [Id], [Checkpoint]
    FROM [dbo].[NaturalKeys]
    WHERE [TypeId] = @TypeId AND [ValueHash] = @ValueHash AND [IsRemoved] = 0 AND [SerializedValue] = @SerializedValue;

    IF NOT EXISTS (SELECT * FROM @NaturalKeys)
    BEGIN
        BEGIN TRY
            INSERT INTO [dbo].[NaturalKeys] ([TypeId], [Checkpoint], [SerializedValue])
            OUTPUT inserted.[Id], inserted.[Checkpoint] INTO @NaturalKeys
            SELECT @TypeId, COALESCE(MAX([Checkpoint]), 0) + CONVERT(BIGINT, 1), @SerializedValue
            FROM [dbo].[NaturalKeys]
            WHERE [TypeId] = @TypeId;
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER() NOT IN (1205, 2601, 2627) OR @Attempt >= 100
                THROW;

            SET @Attempt += 1;
        END CATCH
    END
END

SELECT [Id], [Checkpoint]
FROM @NaturalKeys;
GO

-- As in version 1: the removal moves the record to the latest checkpoint for its type so that synchronizing identity
-- maps see it. It now finds the record by its index and tries again here when a concurrent writer takes the checkpoint
-- or a deadlock chooses it, instead of failing back to the caller.
CREATE OR ALTER PROCEDURE [dbo].[RemoveNaturalKey]
    @Id UNIQUEIDENTIFIER
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TypeId INT = (SELECT [TypeId] FROM [dbo].[NaturalKeys] WHERE [Id] = @Id);
DECLARE @Attempt INT = 1;

WHILE 1 = 1
BEGIN
    BEGIN TRY
        UPDATE [dbo].[NaturalKeys]
        SET [IsRemoved] = 1, [Checkpoint] = (SELECT COALESCE(MAX([Checkpoint]), 0) + CONVERT(BIGINT, 1) FROM [dbo].[NaturalKeys] WHERE [TypeId] = @TypeId)
        WHERE [Id] = @Id AND [IsRemoved] = 0;

        RETURN;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (1205, 2627) OR @Attempt >= 100
            THROW;

        SET @Attempt += 1;
    END CATCH
END
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Versions] WHERE [Version] = 3)
INSERT INTO [dbo].[Versions] ([Version]) VALUES (3);
GO
