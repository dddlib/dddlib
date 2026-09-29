-- dddlib.Persistence: memento repository. Requires 01-SqlServerPersistence.sql.

IF OBJECT_ID('[dbo].[Mementos]') IS NULL
CREATE TABLE [dbo].[Mementos]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TypeId] INT NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    [State] VARCHAR(36) NOT NULL CHECK (DATALENGTH([State]) > 0),
    CONSTRAINT [PK_Memento] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_MementoTypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

CREATE OR ALTER PROCEDURE [dbo].[LoadMemento]
    @Id UNIQUEIDENTIFIER
AS
SET NOCOUNT ON;

SELECT [Id], [TypeId], [Payload], [State]
FROM [dbo].[Mementos]
WHERE [Id] = @Id;
GO

CREATE OR ALTER PROCEDURE [dbo].[SaveMemento]
    @Id UNIQUEIDENTIFIER,
    @TypeId INT,
    @Payload NVARCHAR(MAX),
    @PreCommitState VARCHAR(36),
    @PostCommitState VARCHAR(36) = NULL OUTPUT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

BEGIN TRANSACTION;

    DECLARE @Lock INT;

    EXEC @Lock = sp_getapplock @Resource = @Id, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for memento.', 1;

    DECLARE @Changes TABLE ([Action] NVARCHAR(10));

    SET @PostCommitState = LEFT(NEWID(), 8);

    MERGE [dbo].[Mementos] AS [Target]
    USING (SELECT @Id AS [Id], @TypeId AS [TypeId], @Payload AS [Payload], @PreCommitState AS [State]) AS [Source]
    ON [Target].[Id] = [Source].[Id]
    WHEN MATCHED AND [Target].[State] = [Source].[State] THEN
        UPDATE SET
            [Target].[TypeId] = [Source].[TypeId],
            [Target].[Payload] = [Source].[Payload],
            [Target].[State] = @PostCommitState
    WHEN NOT MATCHED BY TARGET AND [Source].[State] IS NULL THEN
        INSERT ([Id], [TypeId], [Payload], [State])
        VALUES ([Source].[Id], [Source].[TypeId], [Source].[Payload], @PostCommitState)
    OUTPUT $action INTO @Changes;

    IF NOT EXISTS (SELECT 1 FROM @Changes)
        THROW 50409, 'Concurrency error (client side). Commit state mismatch.', 1;

COMMIT TRANSACTION;
GO
