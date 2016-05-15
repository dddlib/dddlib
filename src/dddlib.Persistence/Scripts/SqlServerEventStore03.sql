CREATE TYPE [dbo].[Events] AS TABLE
(
    [Index] INT NOT NULL,
    [PayloadTypeName] VARCHAR(511) NOT NULL,
    [Metadata] VARCHAR(MAX) NOT NULL,
    [Payload] VARCHAR(MAX) NOT NULL
);
GO

CREATE NONCLUSTERED INDEX IX_Event_StreamId
    ON [dbo].[Events] (StreamId); 
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

MERGE INTO [dbo].[Types] WITH (HOLDLOCK) AS [Target]
USING (SELECT DISTINCT [PayloadTypeName] AS [Name] FROM @Events) AS [Source]
ON [Target].[Name] = [Source].[Name]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Name])
    VALUES ([Source].[Name]);

DECLARE @Lock INT;
DECLARE @CommitState VARCHAR(36);

BEGIN TRANSACTION

    -- LINK (Cameron): http://rusanu.com/2015/03/06/the-cost-of-a-transactions-that-has-only-applocks/
    EXEC @Lock = tempdb..sp_getapplock @Resource = @StreamId, @LockMode = 'Exclusive', @LockTimeout = 1000;
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

    DECLARE @NonsenseEvents TABLE ([SequenceNumber] BIGINT, [StreamRevision] INT, [State] VARCHAR(36));

    INSERT INTO [dbo].[Events] WITH (TABLOCKX) ([StreamId], [StreamRevision], [TypeId], [Metadata], [Payload], [CorrelationId], [SequenceNumber])
    OUTPUT inserted.[SequenceNumber], inserted.[StreamRevision], inserted.[State] INTO @NonsenseEvents
    SELECT
        @StreamId,
        (COALESCE([Stream].[Revision], 0) + ROW_NUMBER() OVER (ORDER BY (SELECT NULL))),
        [Type].[Id],
        [Event].[Metadata],
        [Event].[Payload],
        @CorrelationId,
        (COALESCE([Max].[SequenceNumber], 0) + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)))
    FROM @Events [Event] 
        CROSS JOIN (SELECT MAX([SequenceNumber]) AS [SequenceNumber] FROM [dbo].[Events]) [Max]
        CROSS JOIN (SELECT MAX([StreamRevision]) AS [Revision] FROM [dbo].[Events] WHERE [StreamId] = @StreamId) [Stream]
        INNER JOIN [dbo].[Types] [Type] ON [Event].[PayloadTypeName] = [Type].[Name]
    ORDER BY [Event].[Index];

COMMIT TRANSACTION

SELECT @PostCommitState = [State]
FROM @NonsenseEvents
WHERE [StreamRevision] = (SELECT MAX([StreamRevision]) FROM @NonsenseEvents);

GO
