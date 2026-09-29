-- dddlib.Persistence: snapshot store. Requires 01-SqlServerPersistence.sql.

IF OBJECT_ID('[dbo].[Snapshots]') IS NULL
CREATE TABLE [dbo].[Snapshots]
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [StreamRevision] INT NOT NULL,
    [TypeId] INT NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_Snapshot] PRIMARY KEY ([StreamId]),
    CONSTRAINT [FK_SnapshotTypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

CREATE OR ALTER PROCEDURE [dbo].[GetSnapshot]
    @StreamId UNIQUEIDENTIFIER
AS
SET NOCOUNT ON;

SELECT [Snapshot].[StreamId], [Snapshot].[StreamRevision], [Type].[Name] AS [PayloadTypeName], [Snapshot].[Payload]
FROM [dbo].[Snapshots] [Snapshot] INNER JOIN [dbo].[Types] [Type] ON [Snapshot].[TypeId] = [Type].[Id]
WHERE [Snapshot].[StreamId] = @StreamId;
GO

CREATE OR ALTER PROCEDURE [dbo].[PutSnapshot]
    @StreamId UNIQUEIDENTIFIER,
    @StreamRevision INT,
    @PayloadTypeName VARCHAR(511),
    @Payload NVARCHAR(MAX)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Register the type atomically: two concurrent callers must not both insert it.
MERGE INTO [dbo].[Types] WITH (HOLDLOCK) AS [Target]
USING (SELECT @PayloadTypeName AS [Name]) AS [Source]
ON [Target].[Name] = [Source].[Name] COLLATE SQL_Latin1_General_CP1_CS_AS
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Name])
    VALUES ([Source].[Name]);

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
