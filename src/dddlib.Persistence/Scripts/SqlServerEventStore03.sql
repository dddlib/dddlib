CREATE TYPE [dbo].[Events] AS TABLE
(
    [Index] INT NOT NULL,
    [TypeId] INT NOT NULL,
    [Metadata] VARCHAR(MAX) NULL,
    [Payload] VARCHAR(MAX) NOT NULL
);
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
    CONSTRAINT [FK_EventTypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id]),
    CONSTRAINT [IX_StreamRevision] UNIQUE NONCLUSTERED ([StreamLinkId], [StreamRevision])
);
GO

CREATE INDEX [IX_Event_TypeId] ON [dbo].[Events] ([TypeId]);
GO

-- LINK (Cameron): http://sqlmag.com/sql-server/sequence-and-identity-performance
CREATE SEQUENCE [dbo].[SequenceNumber] AS BIGINT
    START WITH 1
    INCREMENT BY 1
    NO CACHE;
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
    @CorrelationId UNIQUEIDENTIFIER,
    @PreCommitState VARCHAR(36),
    @PostCommitState VARCHAR(36) = NULL OUTPUT
AS

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

DECLARE @Lock INT;
DECLARE @EventsCount INT;
DECLARE @StreamLinkId BIGINT;
DECLARE @StreamRevision INT;
DECLARE @State VARCHAR(36);

BEGIN TRANSACTION

    -- LINK (Cameron): http://rusanu.com/2015/03/06/the-cost-of-a-transactions-that-has-only-applocks/
    EXEC @Lock = [tempdb]..[sp_getapplock] @Resource = @StreamId, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for stream.', 1;

    SELECT @StreamLinkId = [LinkId], @StreamRevision = [Revision], @State = [State]
    FROM [dbo].[Streams] 
    WHERE [Id] = @StreamId;

    -- LINK (Cameron): http://blogs.msdn.com/b/manub22/archive/2013/12/31/new-throw-statement-in-sql-server-2012-vs-raiserror.aspx
    IF NOT ISNULL(@PreCommitState, '') = ISNULL(@State, '')
        THROW 50409, 'Concurrency error (client side). Commit state mismatch.', 1;

    SET @PostCommitState = LEFT(NEWID(), 8);
    SET @EventsCount = (SELECT COUNT([Payload]) FROM @Events);

    IF @PreCommitState IS NULL
    BEGIN
        INSERT INTO [dbo].[Streams] ([Id], [Revision], [State])
        --OUTPUT inserted.[LinkId] INTO @StreamLinkId
        VALUES (@StreamId, @EventsCount, @PostCommitState);
        SET @StreamLinkId = SCOPE_IDENTITY();
        SET @StreamRevision = 0;
    END
    ELSE
    BEGIN
        UPDATE [dbo].[Streams] -- ([Id], [Revision], [State])
        SET [Revision] = @StreamRevision + @EventsCount, [State] = @PostCommitState
        WHERE [LinkId] = @StreamLinkId;
    END

    INSERT INTO [dbo].[Events] ([StreamLinkId], [StreamRevision], [TypeId], [Metadata], [Payload], [CorrelationId], [SequenceNumber])
    SELECT
        @StreamLinkId,
        (COALESCE(@StreamRevision, 0) + ROW_NUMBER() OVER (ORDER BY (SELECT NULL))),
        [TypeId],
        [Metadata],
        [Payload],
        @CorrelationId,
        NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Index] ASC)
    FROM @Events
    ORDER BY [Index];

COMMIT TRANSACTION

GO
