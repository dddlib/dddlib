-- dddlib.Persistence: natural key repository. Requires 01-SqlServerPersistence.sql.

IF OBJECT_ID('[dbo].[NaturalKeys]') IS NULL
CREATE TABLE [dbo].[NaturalKeys]
(
    [Id] [uniqueidentifier] NOT NULL CHECK ([Id] != 0x0) DEFAULT NEWSEQUENTIALID(),
    [TypeId] [int] NOT NULL,
    [Checkpoint] [bigint] NOT NULL,
    [SerializedValue] [nvarchar](MAX) NOT NULL CHECK (DATALENGTH([SerializedValue]) > 0),
    [IsRemoved] [bit] NOT NULL DEFAULT 0,
    CONSTRAINT [PK_NaturalKey] PRIMARY KEY CLUSTERED ([TypeId], [Checkpoint]),
    CONSTRAINT [FK_NaturalKeyTypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

CREATE OR ALTER PROCEDURE [dbo].[GetNaturalKeys]
    @AggregateRootTypeName VARCHAR(511),
    @Checkpoint BIGINT
AS
SET NOCOUNT ON;

SELECT [NaturalKey].[Id], [NaturalKey].[SerializedValue], [NaturalKey].[Checkpoint], [NaturalKey].[IsRemoved]
FROM [dbo].[NaturalKeys] [NaturalKey] INNER JOIN [dbo].[Types] [Type] ON [NaturalKey].[TypeId] = [Type].[Id]
WHERE [NaturalKey].[Checkpoint] > @Checkpoint AND [Type].[Name] = @AggregateRootTypeName
ORDER BY [NaturalKey].[Checkpoint];
GO

CREATE OR ALTER PROCEDURE [dbo].[TryAddNaturalKey]
    @AggregateRootTypeName VARCHAR(511),
    @SerializedValue NVARCHAR(MAX),
    @Checkpoint BIGINT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Register the type atomically: two concurrent callers must not both insert it.
MERGE INTO [dbo].[Types] WITH (HOLDLOCK) AS [Target]
USING (SELECT @AggregateRootTypeName AS [Name]) AS [Source]
ON [Target].[Name] = [Source].[Name] COLLATE SQL_Latin1_General_CP1_CS_AS
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Name])
    VALUES ([Source].[Name]);

DECLARE @TypeId INT = (SELECT [Id] FROM [dbo].[Types] WHERE [Name] = @AggregateRootTypeName COLLATE SQL_Latin1_General_CP1_CS_AS);

DECLARE @NaturalKeys TABLE ([Id] uniqueidentifier, [Checkpoint] bigint);

-- A stale checkpoint means the caller has not seen every key yet; it must synchronize and retry.
-- Two callers racing past this check collide on the primary key, which the caller treats the same way.
IF ((SELECT COALESCE(MAX([Checkpoint]), 0) FROM [dbo].[NaturalKeys] WHERE [TypeId] = @TypeId) = @Checkpoint)
INSERT INTO [dbo].[NaturalKeys] ([TypeId], [Checkpoint], [SerializedValue])
OUTPUT inserted.[Id], inserted.[Checkpoint] INTO @NaturalKeys
SELECT @TypeId, @Checkpoint + CONVERT(BIGINT, 1), @SerializedValue;

SELECT [Id], [Checkpoint]
FROM @NaturalKeys;
GO

CREATE OR ALTER PROCEDURE [dbo].[RemoveNaturalKey]
    @Id UNIQUEIDENTIFIER
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TypeId INT = (SELECT [TypeId] FROM [dbo].[NaturalKeys] WHERE [Id] = @Id);

-- The removal moves the record to the latest checkpoint for its type so that synchronizing identity maps see it.
UPDATE [dbo].[NaturalKeys]
SET [IsRemoved] = 1, [Checkpoint] = (SELECT COALESCE(MAX([Checkpoint]), 0) + CONVERT(BIGINT, 1) FROM [dbo].[NaturalKeys] WHERE [TypeId] = @TypeId)
WHERE [Id] = @Id AND [IsRemoved] = 0;
GO
