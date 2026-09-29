-- dddlib.Persistence: memento repository. Requires 01-SqlServerPersistence.sql and 03-SqlServerEventStore.sql.

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

-- Appends the events of a memento save to the aggregate root's stream so that the event dispatcher can deliver them.
-- The memento's state token is authoritative for concurrency: the stream's token is not checked, it is set to @State,
-- the token the memento is being saved with, and the stream revision advances by the number of events. Call it inside
-- the transaction that writes the memento, so that a dispatcher never sees events for a memento that was not saved.
CREATE OR ALTER PROCEDURE [dbo].[AppendEvents]
    @StreamId UNIQUEIDENTIFIER,
    @Events [dbo].[EventList] READONLY,
    @State VARCHAR(36),
    @Metadata NVARCHAR(MAX) = NULL,
    @CorrelationId UNIQUEIDENTIFIER = NULL
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
    EXEC @Lock = sp_getapplock @Resource = 'dddlib.Events.Commit', @LockMode = 'Exclusive', @LockTimeout = 10000;
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
        @Metadata,
        [Event].[Payload],
        @Correlation,
        NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Event].[Index] ASC)
    FROM @Events [Event] CROSS JOIN @Stream [Stream]
    ORDER BY [Event].[Index];

COMMIT TRANSACTION;
GO

CREATE OR ALTER PROCEDURE [dbo].[SaveMemento]
    @Id UNIQUEIDENTIFIER,
    @TypeId INT,
    @Payload NVARCHAR(MAX),
    @PreCommitState VARCHAR(36),
    @Events [dbo].[EventList] READONLY,
    @Metadata NVARCHAR(MAX) = NULL,
    @CorrelationId UNIQUEIDENTIFIER = NULL,
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

    -- The uncommitted events, if any, go into the aggregate root's stream in this transaction. They are not used for
    -- reconstitution; the memento remains the source of state.
    EXEC [dbo].[AppendEvents] @StreamId = @Id, @Events = @Events, @State = @PostCommitState, @Metadata = @Metadata, @CorrelationId = @CorrelationId;

COMMIT TRANSACTION;
GO
