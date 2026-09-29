-- dddlib.Persistence: event store. Requires 01-SqlServerPersistence.sql.

IF TYPE_ID('[dbo].[EventList]') IS NULL
CREATE TYPE [dbo].[EventList] AS TABLE
(
    [Index] INT NOT NULL,
    [TypeId] INT NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    PRIMARY KEY CLUSTERED ([Index] ASC)
);
GO

IF OBJECT_ID('[dbo].[Streams]') IS NULL
CREATE TABLE [dbo].[Streams]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [LinkId] BIGINT NOT NULL IDENTITY,
    [Revision] INT NOT NULL,
    [State] VARCHAR(36) NOT NULL CHECK (DATALENGTH([State]) > 0),
    CONSTRAINT [PK_Stream] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UX_StreamLinkId] UNIQUE ([LinkId])
);
GO

IF OBJECT_ID('[dbo].[Events]') IS NULL
CREATE TABLE [dbo].[Events]
(
    [StreamLinkId] BIGINT NOT NULL,
    [StreamRevision] INT NOT NULL,
    [TypeId] INT NOT NULL,
    [Metadata] NVARCHAR(MAX) NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [SequenceNumber] BIGINT NOT NULL,
    CONSTRAINT [PK_Event] PRIMARY KEY CLUSTERED ([SequenceNumber]),
    CONSTRAINT [FK_EventStreamLinkId_StreamLinkId] FOREIGN KEY ([StreamLinkId]) REFERENCES [dbo].[Streams] ([LinkId]),
    CONSTRAINT [FK_EventTypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Events]') AND name = N'IX_StreamRevision')
CREATE UNIQUE NONCLUSTERED INDEX [IX_StreamRevision] ON [dbo].[Events] ([StreamLinkId], [StreamRevision])
INCLUDE ([TypeId], [Payload], [SequenceNumber]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Events]') AND name = N'IX_Event_TypeId')
CREATE INDEX [IX_Event_TypeId] ON [dbo].[Events] ([TypeId]);
GO

IF OBJECT_ID('[dbo].[SequenceNumber]', 'SO') IS NULL
CREATE SEQUENCE [dbo].[SequenceNumber] AS BIGINT
    START WITH 1
    INCREMENT BY 1;
GO

CREATE OR ALTER PROCEDURE [dbo].[GetStream]
    @StreamId UNIQUEIDENTIFIER,
    @StreamRevision INT,
    @State VARCHAR(36) = NULL OUTPUT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 1000;

-- The stream row is read and held so that the state and the events come from the same committed version.
BEGIN TRANSACTION;

    DECLARE @StreamLinkId BIGINT;

    SELECT @StreamLinkId = [LinkId], @State = [State]
    FROM [dbo].[Streams] WITH (HOLDLOCK)
    WHERE [Id] = @StreamId;

    SELECT [StreamRevision], [TypeId], [Payload], [SequenceNumber]
    FROM [dbo].[Events]
    WHERE [StreamLinkId] = @StreamLinkId
        AND [StreamRevision] > @StreamRevision
    ORDER BY [StreamRevision];

COMMIT TRANSACTION;
GO

CREATE OR ALTER PROCEDURE [dbo].[CommitStream]
    @StreamId UNIQUEIDENTIFIER,
    @Events [dbo].[EventList] READONLY,
    @Metadata NVARCHAR(MAX),
    @CorrelationId UNIQUEIDENTIFIER,
    @PreCommitState VARCHAR(36),
    @PostCommitState VARCHAR(36) = NULL OUTPUT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

BEGIN TRANSACTION;

    DECLARE @Lock INT;

    EXEC @Lock = sp_getapplock @Resource = @StreamId, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for stream.', 1;

    DECLARE @StreamRevision INT = 0;
    DECLARE @Stream TABLE ([LinkId] BIGINT, [Revision] INT);

    SET @PostCommitState = LEFT(NEWID(), 8);

    MERGE INTO [dbo].[Streams] AS [Target]
    USING (
        SELECT @StreamId AS [Id], COUNT([Index]) AS [EventCount], @PreCommitState AS [State], @PostCommitState AS [NewState]
        FROM @Events) AS [Source]
    ON [Target].[Id] = [Source].[Id]
    WHEN MATCHED AND [Target].[State] = [Source].[State] THEN
        UPDATE SET [Target].[Revision] = [Target].[Revision] + [Source].[EventCount], [Target].[State] = [Source].[NewState], @StreamRevision = [Target].[Revision]
    WHEN NOT MATCHED BY TARGET AND [Source].[State] IS NULL THEN
        INSERT ([Id], [Revision], [State])
        VALUES ([Source].[Id], [Source].[EventCount], [Source].[NewState])
    OUTPUT inserted.[LinkId], inserted.[Revision] INTO @Stream;

    IF NOT EXISTS (SELECT 1 FROM @Stream)
        THROW 50409, 'Concurrency error (client side). Commit state mismatch.', 1;

    INSERT INTO [dbo].[Events] ([StreamLinkId], [StreamRevision], [TypeId], [Metadata], [Payload], [CorrelationId], [SequenceNumber])
    SELECT
        [Stream].[LinkId],
        @StreamRevision + ROW_NUMBER() OVER (ORDER BY [Event].[Index]),
        [Event].[TypeId],
        @Metadata,
        [Event].[Payload],
        @CorrelationId,
        NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Event].[Index] ASC)
    FROM @Events [Event] CROSS JOIN @Stream [Stream]
    ORDER BY [Event].[Index];

COMMIT TRANSACTION;
GO
