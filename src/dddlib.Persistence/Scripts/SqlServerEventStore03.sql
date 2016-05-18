-- LINK (Cameron): https://msdn.microsoft.com/en-us/library/mt718711.aspx
-- NOTE (Cameron): Memory optimized temp tables are only supported in SQL Server 2016+ so I'm leaving it out here.
CREATE TYPE [dbo].[Events] AS TABLE
(
    [Index] INT NOT NULL,
    [TypeId] INT NOT NULL,
    [Payload] VARCHAR(MAX) NOT NULL,
    PRIMARY KEY CLUSTERED ([Index] ASC)
); --WITH (MEMORY_OPTIMIZED = ON);
GO

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

DROP TABLE [dbo].[Events];
GO

CREATE TABLE [dbo].[Events]
(
    [StreamLinkId] BIGINT NOT NULL,
    [StreamRevision] INT NOT NULL,
    [TypeId] INT NOT NULL, -- this allows us to know which type to reconstitute
    [Metadata] VARCHAR(MAX) NULL,
    [Payload] VARCHAR(MAX) NOT NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [SequenceNumber] BIGINT NOT NULL,
    CONSTRAINT [PK_Event] PRIMARY KEY CLUSTERED ([SequenceNumber]),
    CONSTRAINT [FK_EventStreamLinkId_StreamLinkId] FOREIGN KEY ([StreamLinkId]) REFERENCES [dbo].[Streams] ([LinkId]),
    CONSTRAINT [FK_EventTypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_StreamRevision] ON [dbo].[Events] ([StreamLinkId], [StreamRevision])
INCLUDE ([TypeId], [Payload], [SequenceNumber]);
GO

CREATE INDEX [IX_Event_TypeId] ON [dbo].[Events] ([TypeId]);
GO

-- LINK (Cameron): http://sqlmag.com/sql-server/sequence-and-identity-performance
CREATE SEQUENCE [dbo].[SequenceNumber] AS BIGINT
    START WITH 1
    INCREMENT BY 1;
    --NO CACHE;
GO

ALTER PROCEDURE [dbo].[GetStream]
    @StreamId UNIQUEIDENTIFIER,
    @StreamRevision BIGINT,
    @State VARCHAR(36) OUTPUT
AS
SET NOCOUNT ON;

DECLARE @Lock INT;
DECLARE @StreamLinkId BIGINT;

-- LINK (Cameron): http://rusanu.com/2015/03/06/the-cost-of-a-transactions-that-has-only-applocks/
-- LINK (Cameron): https://technet.microsoft.com/en-us/library/ms175519(v=sql.105).aspx
EXEC @Lock = [tempdb]..[sp_getapplock] @Resource = @StreamId, @LockMode = 'Shared', @LockOwner = 'Session', @LockTimeout = 1000;
IF @Lock < 0
    THROW 50500, 'Timeout (server side). Failed to acquire read lock for stream.', 1;

SELECT @StreamLinkId = [LinkId], @State = [State]
FROM [dbo].[Streams]
WHERE [Id] = @StreamId;

SELECT [StreamRevision], [TypeId], [Payload], [SequenceNumber]
FROM [dbo].[Events]
WHERE [StreamLinkId] = @StreamLinkId
    AND [StreamRevision] > @StreamRevision
ORDER BY [StreamRevision];

EXEC @Lock = [tempdb]..[sp_releaseapplock] @Resource = @StreamId, @LockOwner = 'Session';

GO

ALTER PROCEDURE [dbo].[CommitStream]
    @StreamId UNIQUEIDENTIFIER,
    @Events [dbo].[Events] READONLY,
    @Metadata VARCHAR(MAX),
    @CorrelationId UNIQUEIDENTIFIER,
    @PreCommitState VARCHAR(36),
    @PostCommitState VARCHAR(36) = NULL OUTPUT
AS

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

BEGIN TRANSACTION

    DECLARE @Lock INT;

    -- LINK (Cameron): http://rusanu.com/2015/03/06/the-cost-of-a-transactions-that-has-only-applocks/
    EXEC @Lock = [tempdb]..[sp_getapplock] @Resource = @StreamId, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for stream.', 1;

    DECLARE @StreamRevision INT = 0;
    DECLARE @Stream TABLE
    (
        [LinkId] BIGINT,
        [Revision] INT
    );

    SET @PostCommitState = LEFT(NEWID(), 8);

    MERGE INTO [dbo].[Streams] AS [Target]
    USING (
        SELECT @StreamId AS [Id], COUNT([Index]) AS [EventCount], @PreCommitState AS [State], @PostCommitState AS [NewState]
        FROM @Events) AS [Source]
    ON [Target].[Id] = [Source].[Id]
    WHEN MATCHED AND [Target].[State] = [Source].[State]
    THEN UPDATE
        SET [Target].[Revision] = [Target].[Revision] + [Source].[EventCount], [Target].[State] = [Source].[NewState], @StreamRevision = [Target].[Revision]
    WHEN NOT MATCHED BY TARGET AND [Source].[State] IS NULL THEN
        INSERT ([Id], [Revision], [State])
        VALUES ([Source].[Id], [Source].[EventCount], [Source].[NewState])
    OUTPUT inserted.[LinkId], inserted.[Revision]
    INTO @Stream;

    -- LINK (Cameron): http://blogs.msdn.com/b/manub22/archive/2013/12/31/new-throw-statement-in-sql-server-2012-vs-raiserror.aspx
    IF NOT EXISTS (SELECT 1 FROM @Stream)
        THROW 50409, 'Concurrency error (client side). Commit state mismatch.', 1;

    INSERT INTO [dbo].[Events] ([StreamLinkId], [StreamRevision], [TypeId], [Metadata], [Payload], [CorrelationId], [SequenceNumber])
    SELECT
        [Stream].[LinkId],
        (@StreamRevision + ROW_NUMBER() OVER (ORDER BY (SELECT NULL))),
        [Event].[TypeId],
        @Metadata,
        [Event].[Payload],
        @CorrelationId,
        NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Event].[Index] ASC)
    FROM @Events [Event] CROSS JOIN @Stream [Stream]
    ORDER BY [Event].[Index];

COMMIT TRANSACTION

GO

CREATE PROCEDURE [dbo].[CommitStream2]
    @StreamId UNIQUEIDENTIFIER,
    @TypeId INT,
    @Metadata VARCHAR(MAX),
    @Payload VARCHAR(MAX),
    @CorrelationId UNIQUEIDENTIFIER,
    @PreCommitState VARCHAR(36),
    @PostCommitState VARCHAR(36) = NULL OUTPUT
AS

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

BEGIN TRANSACTION

    DECLARE @Lock INT;

    -- LINK (Cameron): http://rusanu.com/2015/03/06/the-cost-of-a-transactions-that-has-only-applocks/
    EXEC @Lock = [tempdb]..[sp_getapplock] @Resource = @StreamId, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for stream.', 1;

    DECLARE @StreamRevision INT = 0;
    DECLARE @Stream TABLE
    (
        [LinkId] BIGINT,
        [Revision] INT
    );

    SET @PostCommitState = LEFT(NEWID(), 8);

    MERGE INTO [dbo].[Streams] AS [Target]
    USING (SELECT @StreamId AS [Id], 1 AS [EventCount], @PreCommitState AS [State], @PostCommitState AS [NewState]) AS [Source]
    ON [Target].[Id] = [Source].[Id]
    WHEN MATCHED AND [Target].[State] = [Source].[State]
    THEN UPDATE
        SET [Target].[Revision] = [Target].[Revision] + [Source].[EventCount], [Target].[State] = [Source].[NewState], @StreamRevision = [Target].[Revision]
    WHEN NOT MATCHED BY TARGET AND [Source].[State] IS NULL THEN
        INSERT ([Id], [Revision], [State])
        VALUES ([Source].[Id], [Source].[EventCount], [Source].[NewState])
    OUTPUT inserted.[LinkId], inserted.[Revision]
    INTO @Stream;

    -- LINK (Cameron): http://blogs.msdn.com/b/manub22/archive/2013/12/31/new-throw-statement-in-sql-server-2012-vs-raiserror.aspx
    IF NOT EXISTS (SELECT 1 FROM @Stream)
        THROW 50409, 'Concurrency error (client side). Commit state mismatch.', 1;

    INSERT INTO [dbo].[Events] ([StreamLinkId], [StreamRevision], [TypeId], [Metadata], [Payload], [CorrelationId], [SequenceNumber])
    SELECT
        [Stream].[LinkId],
        @StreamRevision + 1,
        @TypeId,
        @Metadata,
        @Payload,
        @CorrelationId,
        NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Stream].[LinkId] ASC)
    FROM @Stream [Stream];

COMMIT TRANSACTION

GO
