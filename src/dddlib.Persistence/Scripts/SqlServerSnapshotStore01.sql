CREATE TABLE [dbo].[Snapshots]
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [StreamRevision] INT NOT NULL,
    [TypeId] INT NOT NULL,
    [Payload] VARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_Snapshot] PRIMARY KEY ([StreamId]),
    CONSTRAINT [FK_TypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

CREATE PROCEDURE [dbo].[GetSnapshot]
    @StreamId VARCHAR(511)
AS
SET NOCOUNT ON;

SELECT [Snapshot].[StreamId], [Snapshot].[StreamRevision], [Type].[Name] AS [PayloadTypeName], [Snapshot].[Payload]
FROM [dbo].[Snapshots] [Snapshot] INNER JOIN [dbo].[Types] [Type] ON [Snapshot].[TypeId] = [Type].[Id]
WHERE [Snapshot].[StreamId] = @StreamId;

GO

CREATE PROCEDURE [dbo].[PutSnapshot]
    @StreamId UNIQUEIDENTIFIER,
    @StreamRevision INT,
    @PayloadTypeName VARCHAR(511),
    @Payload VARCHAR(MAX)
AS
SET NOCOUNT ON;

DECLARE @TypeId INT = (
    SELECT [Id]
    FROM [dbo].[Types]
    WHERE [Name] = @PayloadTypeName
);

IF @TypeId IS NULL
BEGIN
    INSERT INTO [dbo].[Types] ([Name])
    VALUES (@PayloadTypeName);
    SET @TypeId = SCOPE_IDENTITY();
END

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

CREATE PROCEDURE [dbo].[DeleteAllSnapshots]
AS
SET NOCOUNT ON;

TRUNCATE TABLE [dbo].[Snapshots];

GO