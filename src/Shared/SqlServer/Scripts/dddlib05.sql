-- dddlib SQL Server schema, version 5: concurrent first registrations of different types no longer deadlock.
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

-- As in version 3, registering the type with TryAddType.
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
