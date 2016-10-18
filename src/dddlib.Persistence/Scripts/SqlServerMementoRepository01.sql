CREATE TABLE [dbo].[Mementos]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TypeId] INT NOT NULL,
    [Payload] VARCHAR(MAX) NOT NULL,
    --[IsDestroyed] BIT NOT NULL DEFAULT(0),
    [State] VARCHAR(36) NOT NULL CHECK (DATALENGTH([State]) > 0),
    CONSTRAINT [PK_Memento] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_MementoTypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

CREATE PROCEDURE [dbo].[LoadMemento]
    @Id UNIQUEIDENTIFIER
AS
SET NOCOUNT ON;

DECLARE @Lock INT;

-- LINK (Cameron): http://rusanu.com/2015/03/06/the-cost-of-a-transactions-that-has-only-applocks/
-- LINK (Cameron): https://technet.microsoft.com/en-us/library/ms175519(v=sql.105).aspx
EXEC @Lock = [tempdb]..[sp_getapplock] @Resource = @Id, @LockMode = 'Shared', @LockOwner = 'Session', @LockTimeout = 1000;
IF @Lock < 0
    THROW 50500, 'Timeout (server side). Failed to acquire read lock for memento.', 1;

SELECT [Memento].[Id], [Memento].[TypeId], [Memento].[Payload], [Memento].[State]
FROM [dbo].[Mementos] [Memento]
WHERE [Memento].[Id] = @Id;
    --AND [Memento].[IsDestroyed] = 0;

EXEC @Lock = [tempdb]..[sp_releaseapplock] @Resource = @Id, @LockOwner = 'Session';

GO

CREATE PROCEDURE [dbo].[SaveMemento]
    @Id UNIQUEIDENTIFIER,
    @TypeId INT,
    @Payload VARCHAR(MAX),
    --@IsDestroyed BIT,
    @PreCommitState VARCHAR(36),
    @PostCommitState VARCHAR(36) = NULL OUTPUT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

BEGIN TRANSACTION

    DECLARE @Lock INT;

    -- LINK (Cameron): http://rusanu.com/2015/03/06/the-cost-of-a-transactions-that-has-only-applocks/
    EXEC @Lock = [tempdb]..[sp_getapplock] @Resource = @Id, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for memento.', 1;

    DECLARE @Changes TABLE
    (
        [Action] NVARCHAR(10)
    );

    SET @PostCommitState = LEFT(NEWID(), 8);

    MERGE [dbo].[Mementos] AS [Target]
    USING (SELECT @Id AS [Id], @TypeId AS [TypeId], @Payload AS [Payload]) AS [Source]
        ON [Target].[Id] = [Source].[Id]
    WHEN MATCHED AND ([Target].[State] = @PreCommitState /* AND [Target].[IsDestroyed] = 0 */) THEN
        UPDATE SET
            [Target].[TypeId] = [Source].[TypeId],
            [Target].[Payload] = [Source].[Payload],
            [Target].[State] = @PostCommitState
    WHEN NOT MATCHED BY TARGET THEN
        INSERT ([Id], [TypeId], [Payload], [State]) 
        VALUES ([Source].[Id], [Source].[TypeId], [Source].[Payload], @PostCommitState)
    OUTPUT $action
    INTO @Changes;

    -- LINK (Cameron): http://blogs.msdn.com/b/manub22/archive/2013/12/31/new-throw-statement-in-sql-server-2012-vs-raiserror.aspx
    IF NOT EXISTS (SELECT 1 FROM @Changes)
        THROW 50409, 'Concurrency error (client side). Commit state mismatch.', 1;

COMMIT TRANSACTION

GO