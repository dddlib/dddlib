-- dddlib SQL Server schema, version 7: each event can carry metadata of its own.
--
-- Applied by SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer), SqlServerEventDispatcherSchema.EnsureAsync
-- (dddlib.Persistence.EventDispatcher.SqlServer) or SqlServerProjectionsSchema.EnsureAsync
-- (dddlib.Persistence.Projections.SqlServer). Additions only: AppendEvents and AppendStreams gain an optional
-- @EventMetadata parameter, and a table-valued parameter that is left out is an empty table, so callers requiring
-- version 6 store what they did before. EventList is unchanged: a table type cannot be altered, and CommitStream and
-- SaveMemento use it too. Idempotent like the earlier versions.
--
-- An event with a row in @EventMetadata is stored with that row's Metadata instead of @Metadata (AppendEvents) or its
-- stream's Metadata (AppendStreams). The client writes each row as the whole JSON object, dddlib's hostname and
-- timestamp and the append's entries included, with the event's own entries merged over them
-- (https://github.com/dddlib/dddlib/issues/61).

IF TYPE_ID('[dbo].[EventMetadataList]') IS NULL
CREATE TYPE [dbo].[EventMetadataList] AS TABLE
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [Index] INT NOT NULL,
    [Metadata] NVARCHAR(MAX) NOT NULL,
    PRIMARY KEY CLUSTERED ([StreamId] ASC, [Index] ASC)
);
GO

-- As in version 1, with each event's own metadata.
CREATE OR ALTER PROCEDURE [dbo].[AppendEvents]
    @StreamId UNIQUEIDENTIFIER,
    @Events [dbo].[EventList] READONLY,
    @State VARCHAR(36),
    @Metadata NVARCHAR(MAX) = NULL,
    @CorrelationId UNIQUEIDENTIFIER = NULL,
    @EventMetadata [dbo].[EventMetadataList] READONLY
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
        COALESCE([Own].[Metadata], @Metadata),
        [Event].[Payload],
        @Correlation,
        NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Event].[Index] ASC)
    FROM @Events [Event]
    CROSS JOIN @Stream [Stream]
    LEFT JOIN @EventMetadata [Own] ON [Own].[StreamId] = @StreamId AND [Own].[Index] = [Event].[Index]
    ORDER BY [Event].[Index];

COMMIT TRANSACTION;
GO

-- As in version 6, with each event's own metadata.
CREATE OR ALTER PROCEDURE [dbo].[AppendStreams]
    @Streams [dbo].[StreamAppendList] READONLY,
    @Events [dbo].[StreamEventList] READONLY,
    @CorrelationId UNIQUEIDENTIFIER,
    @EventMetadata [dbo].[EventMetadataList] READONLY
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

IF @@TRANCOUNT = 0
    THROW 50000, 'AppendStreams must run inside a transaction that holds the identity locks (LockStreams).', 1;

IF NOT EXISTS (SELECT 1 FROM @Events)
    RETURN;

DECLARE @Lock INT;

EXEC @Lock = sp_getapplock @Resource = 'dddlib.Events.Commit', @LockMode = 'Exclusive', @LockTimeout = 10000;
IF @Lock < 0
    THROW 50500, 'Concurrency error (server side). Failed to acquire the event sequence lock.', 1;

DECLARE @Appends TABLE
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Order] INT NOT NULL,
    [IsNew] BIT NOT NULL,
    [Revision] INT NOT NULL,
    [EventCount] INT NOT NULL,
    [State] VARCHAR(36) NOT NULL,
    [Metadata] NVARCHAR(MAX) NULL
);

INSERT INTO @Appends ([StreamId], [Order], [IsNew], [Revision], [EventCount], [State], [Metadata])
SELECT
    [Append].[StreamId],
    [Append].[Order],
    CASE WHEN [Stream].[Id] IS NULL THEN 1 ELSE 0 END,
    COALESCE([Stream].[Revision], 0),
    (SELECT COUNT(*) FROM @Events [Event] WHERE [Event].[StreamId] = [Append].[StreamId]),
    [Append].[State],
    [Append].[Metadata]
FROM @Streams [Append]
LEFT JOIN [dbo].[Streams] [Stream] ON [Stream].[Id] = [Append].[StreamId]
WHERE EXISTS (SELECT 1 FROM @Events [Event] WHERE [Event].[StreamId] = [Append].[StreamId]);

UPDATE [Stream]
SET [Stream].[Revision] = [Stream].[Revision] + [Append].[EventCount], [Stream].[State] = [Append].[State]
FROM [dbo].[Streams] [Stream]
INNER JOIN @Appends [Append] ON [Append].[StreamId] = [Stream].[Id]
WHERE [Append].[IsNew] = 0;

INSERT INTO [dbo].[Streams] ([Id], [Revision], [State])
SELECT [StreamId], [EventCount], [State]
FROM @Appends
WHERE [IsNew] = 1;

INSERT INTO [dbo].[Events] ([StreamLinkId], [StreamRevision], [TypeId], [Metadata], [Payload], [CorrelationId], [SequenceNumber])
SELECT
    [Stream].[LinkId],
    [Append].[Revision] + ROW_NUMBER() OVER (PARTITION BY [Event].[StreamId] ORDER BY [Event].[Index]),
    [Event].[TypeId],
    COALESCE([Own].[Metadata], [Append].[Metadata]),
    [Event].[Payload],
    @CorrelationId,
    NEXT VALUE FOR [dbo].[SequenceNumber] OVER (ORDER BY [Append].[Order] ASC, [Event].[Index] ASC)
FROM @Events [Event]
INNER JOIN @Appends [Append] ON [Append].[StreamId] = [Event].[StreamId]
INNER JOIN [dbo].[Streams] [Stream] ON [Stream].[Id] = [Event].[StreamId]
LEFT JOIN @EventMetadata [Own] ON [Own].[StreamId] = [Event].[StreamId] AND [Own].[Index] = [Event].[Index]
ORDER BY [Append].[Order], [Event].[Index];
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Versions] WHERE [Version] = 7)
INSERT INTO [dbo].[Versions] ([Version]) VALUES (7);
GO
