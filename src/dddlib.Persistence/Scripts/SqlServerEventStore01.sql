CREATE TABLE [dbo].[Events]
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [StreamRevision] INT NOT NULL,
    [TypeId] INT NOT NULL, -- this allows us to know which type to reconstitute
    [Payload] VARCHAR(MAX) NOT NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [SequenceNumber] BIGINT NOT NULL,
    [State] VARCHAR(36) NOT NULL CHECK (DATALENGTH([State]) > 0) DEFAULT LEFT(NEWID(), 8),
    CONSTRAINT [PK_Event] PRIMARY KEY ([SequenceNumber]),
    CONSTRAINT [FK_EventTypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

CREATE PROCEDURE [dbo].[CommitStream]
    @StreamId UNIQUEIDENTIFIER,
    @CorrelationId UNIQUEIDENTIFIER,
    @PreCommitState VARCHAR(36),
    @PostCommitState VARCHAR(36) = NULL OUTPUT
AS
SET NOCOUNT ON;

MERGE INTO [dbo].[Types] WITH (HOLDLOCK) AS [Target]
USING (SELECT DISTINCT [PayloadTypeName] AS [Name] FROM #Events) AS [Source]
ON [Target].[Name] = [Source].[Name]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Name])
    VALUES ([Source].[Name]);

DECLARE @Lock INT;
DECLARE @CommitState VARCHAR(36);

BEGIN TRANSACTION

    EXEC @Lock = sp_getapplock @Resource = @StreamId, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for stream.', 1;

    WITH [Stream]
    AS (
        SELECT [StreamRevision] AS [Revision], [State]
        FROM [dbo].[Events]
        WHERE [StreamId] = @StreamId
    )
    SELECT @CommitState = [State]
    FROM [Stream] 
    WHERE [Revision] = (SELECT MAX([Revision]) FROM [Stream]);

    -- LINK (Cameron): http://blogs.msdn.com/b/manub22/archive/2013/12/31/new-throw-statement-in-sql-server-2012-vs-raiserror.aspx
    IF NOT ISNULL(@PreCommitState, '') = @CommitState
        THROW 50409, 'Concurrency error (client side). Commit state mismatch.', 1;

    DECLARE @Events TABLE ([SequenceNumber] BIGINT, [StreamRevision] INT, [State] VARCHAR(36));

    INSERT INTO [dbo].[Events] WITH (TABLOCKX) ([StreamId], [StreamRevision], [TypeId], [Payload], [CorrelationId], [SequenceNumber])
    OUTPUT inserted.[SequenceNumber], inserted.[StreamRevision], inserted.[State] INTO @Events
    SELECT
        @StreamId,
        (COALESCE([Stream].[Revision], 0) + ROW_NUMBER() OVER (ORDER BY (SELECT NULL))),
        [Type].[Id],
        [Event].[Payload],
        @CorrelationId,
        (COALESCE([Max].[SequenceNumber], 0) + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)))
    FROM #Events [Event] 
        CROSS JOIN (SELECT MAX([SequenceNumber]) AS [SequenceNumber] FROM [dbo].[Events]) [Max]
        CROSS JOIN (SELECT MAX([StreamRevision]) AS [Revision] FROM [dbo].[Events] WHERE [StreamId] = @StreamId) [Stream]
        INNER JOIN [dbo].[Types] [Type] ON [Event].[PayloadTypeName] = [Type].[Name]
    ORDER BY [Event].[Index];

COMMIT TRANSACTION

SELECT @PostCommitState = [State]
FROM @Events
WHERE [StreamRevision] = (SELECT MAX([StreamRevision]) FROM @Events);

GO

CREATE PROCEDURE [dbo].[GetStream]
    @StreamId UNIQUEIDENTIFIER,
    @StreamRevision BIGINT
AS
SET NOCOUNT ON;

SELECT [Event].[StreamId], [Event].[StreamRevision], [Type].[Name] AS [PayloadTypeName], [Event].[Payload], [Event].[SequenceNumber], [Event].[State]
FROM [dbo].[Events] [Event] WITH (NOLOCK) INNER JOIN [dbo].[Types] [Type] ON [Event].[TypeId] = [Type].[Id]
WHERE [Event].[StreamId] = @StreamId
    AND @StreamRevision >= @StreamRevision
ORDER BY [Event].[StreamRevision];

GO
