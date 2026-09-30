-- dddlib SQL Server schema, version 1.
--
-- Applied by SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer) or SqlServerEventDispatcherSchema.EnsureAsync
-- (dddlib.Persistence.EventDispatcher.SqlServer). To run it by hand, create the schema, replace the bracketed dbo schema
-- name throughout with it, and run the scripts in version order. Each script records its own version in the Versions
-- table; released scripts never change, and every later change is a new script. This one is idempotent so that it
-- can adopt a database whose objects were created before versioning.

IF OBJECT_ID('[dbo].[Versions]') IS NULL
CREATE TABLE [dbo].[Versions]
(
    [Version] [int] NOT NULL,
    [Description] [varchar](max) NULL,
    [Script] [nvarchar](max) NULL,
    [AppliedAt] [datetime2] NOT NULL CONSTRAINT [DF_Versions_AppliedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Versions] PRIMARY KEY CLUSTERED ([Version])
);
GO

-- Type registry.

IF OBJECT_ID('[dbo].[Types]') IS NULL
CREATE TABLE [dbo].[Types]
(
    [Id] [int] IDENTITY NOT NULL,
    [Name] [varchar](511) NOT NULL CHECK (DATALENGTH([Name]) > 0),
    CONSTRAINT [PK_Type] PRIMARY KEY CLUSTERED ([Id])
);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Types]') AND name = N'IX_Type_Name')
CREATE UNIQUE INDEX [IX_Type_Name] ON [dbo].[Types] ([Name]);
GO

CREATE OR ALTER PROCEDURE [dbo].[TryAddType]
    @Name VARCHAR(511)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

MERGE INTO [dbo].[Types] WITH (HOLDLOCK) AS [Target]
USING (SELECT @Name AS [Name]) AS [Source]
ON [Target].[Name] = [Source].[Name] COLLATE SQL_Latin1_General_CP1_CS_AS
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Name])
    VALUES ([Source].[Name]);
GO

CREATE OR ALTER PROCEDURE [dbo].[GetTypes]
AS
SET NOCOUNT ON;

SELECT [Id], [Name]
FROM [dbo].[Types]
ORDER BY [Id];
GO

-- Natural keys (identity map).

IF OBJECT_ID('[dbo].[NaturalKeys]') IS NULL
CREATE TABLE [dbo].[NaturalKeys]
(
    [Id] [uniqueidentifier] NOT NULL CHECK ([Id] != 0x0) DEFAULT NEWSEQUENTIALID(),
    [TypeId] [int] NOT NULL,
    [Checkpoint] [bigint] NOT NULL,
    [SerializedValue] [nvarchar](MAX) NOT NULL CHECK (DATALENGTH([SerializedValue]) > 0),
    [IsRemoved] [bit] NOT NULL DEFAULT 0,
    CONSTRAINT [PK_NaturalKey] PRIMARY KEY CLUSTERED ([TypeId], [Checkpoint]),
    CONSTRAINT [FK_NaturalKeyTypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

CREATE OR ALTER PROCEDURE [dbo].[GetNaturalKeys]
    @AggregateRootTypeName VARCHAR(511),
    @Checkpoint BIGINT
AS
SET NOCOUNT ON;

SELECT [NaturalKey].[Id], [NaturalKey].[SerializedValue], [NaturalKey].[Checkpoint], [NaturalKey].[IsRemoved]
FROM [dbo].[NaturalKeys] [NaturalKey] INNER JOIN [dbo].[Types] [Type] ON [NaturalKey].[TypeId] = [Type].[Id]
WHERE [NaturalKey].[Checkpoint] > @Checkpoint AND [Type].[Name] = @AggregateRootTypeName
ORDER BY [NaturalKey].[Checkpoint];
GO

CREATE OR ALTER PROCEDURE [dbo].[TryAddNaturalKey]
    @AggregateRootTypeName VARCHAR(511),
    @SerializedValue NVARCHAR(MAX),
    @Checkpoint BIGINT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Register the type atomically: two concurrent callers must not both insert it.
MERGE INTO [dbo].[Types] WITH (HOLDLOCK) AS [Target]
USING (SELECT @AggregateRootTypeName AS [Name]) AS [Source]
ON [Target].[Name] = [Source].[Name] COLLATE SQL_Latin1_General_CP1_CS_AS
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Name])
    VALUES ([Source].[Name]);

DECLARE @TypeId INT = (SELECT [Id] FROM [dbo].[Types] WHERE [Name] = @AggregateRootTypeName COLLATE SQL_Latin1_General_CP1_CS_AS);

DECLARE @NaturalKeys TABLE ([Id] uniqueidentifier, [Checkpoint] bigint);

-- A stale checkpoint means the caller has not seen every key yet; it must synchronize and retry.
-- Two callers racing past this check collide on the primary key, which the caller treats the same way.
IF ((SELECT COALESCE(MAX([Checkpoint]), 0) FROM [dbo].[NaturalKeys] WHERE [TypeId] = @TypeId) = @Checkpoint)
INSERT INTO [dbo].[NaturalKeys] ([TypeId], [Checkpoint], [SerializedValue])
OUTPUT inserted.[Id], inserted.[Checkpoint] INTO @NaturalKeys
SELECT @TypeId, @Checkpoint + CONVERT(BIGINT, 1), @SerializedValue;

SELECT [Id], [Checkpoint]
FROM @NaturalKeys;
GO

CREATE OR ALTER PROCEDURE [dbo].[RemoveNaturalKey]
    @Id UNIQUEIDENTIFIER
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TypeId INT = (SELECT [TypeId] FROM [dbo].[NaturalKeys] WHERE [Id] = @Id);

-- The removal moves the record to the latest checkpoint for its type so that synchronizing identity maps see it.
UPDATE [dbo].[NaturalKeys]
SET [IsRemoved] = 1, [Checkpoint] = (SELECT COALESCE(MAX([Checkpoint]), 0) + CONVERT(BIGINT, 1) FROM [dbo].[NaturalKeys] WHERE [TypeId] = @TypeId)
WHERE [Id] = @Id AND [IsRemoved] = 0;
GO

-- Event store.

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

    -- Commits are serialized so that sequence numbers are assigned in commit order: a dispatcher that has passed
    -- sequence number N can rely on every event below N being committed or permanently rolled back.
    EXEC @Lock = sp_getapplock @Resource = 'dddlib.Events.Commit', @LockMode = 'Exclusive', @LockTimeout = 10000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire the event sequence lock.', 1;

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

-- Snapshot store.

IF OBJECT_ID('[dbo].[Snapshots]') IS NULL
CREATE TABLE [dbo].[Snapshots]
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [StreamRevision] INT NOT NULL,
    [TypeId] INT NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_Snapshot] PRIMARY KEY ([StreamId]),
    CONSTRAINT [FK_SnapshotTypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

CREATE OR ALTER PROCEDURE [dbo].[GetSnapshot]
    @StreamId UNIQUEIDENTIFIER
AS
SET NOCOUNT ON;

SELECT [Snapshot].[StreamId], [Snapshot].[StreamRevision], [Type].[Name] AS [PayloadTypeName], [Snapshot].[Payload]
FROM [dbo].[Snapshots] [Snapshot] INNER JOIN [dbo].[Types] [Type] ON [Snapshot].[TypeId] = [Type].[Id]
WHERE [Snapshot].[StreamId] = @StreamId;
GO

CREATE OR ALTER PROCEDURE [dbo].[PutSnapshot]
    @StreamId UNIQUEIDENTIFIER,
    @StreamRevision INT,
    @PayloadTypeName VARCHAR(511),
    @Payload NVARCHAR(MAX)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Register the type atomically: two concurrent callers must not both insert it.
MERGE INTO [dbo].[Types] WITH (HOLDLOCK) AS [Target]
USING (SELECT @PayloadTypeName AS [Name]) AS [Source]
ON [Target].[Name] = [Source].[Name] COLLATE SQL_Latin1_General_CP1_CS_AS
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Name])
    VALUES ([Source].[Name]);

DECLARE @TypeId INT = (SELECT [Id] FROM [dbo].[Types] WHERE [Name] = @PayloadTypeName COLLATE SQL_Latin1_General_CP1_CS_AS);

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

-- Memento repository.

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

-- Event dispatcher.

IF OBJECT_ID('[dbo].[Batches]') IS NULL
CREATE TABLE [dbo].[Batches]
(
    [Id] BIGINT IDENTITY NOT NULL,
    [DispatcherId] UNIQUEIDENTIFIER NOT NULL,
    [SequenceNumber] BIGINT NOT NULL,
    [LastSequenceNumber] BIGINT NOT NULL,
    [Timestamp] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    [Complete] BIT NOT NULL DEFAULT 0,
    CONSTRAINT [PK_Batch] PRIMARY KEY CLUSTERED ([Id])
);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Batches]') AND name = N'IX_Batch_Dispatcher')
CREATE INDEX [IX_Batch_Dispatcher] ON [dbo].[Batches] ([DispatcherId], [Complete]) INCLUDE ([LastSequenceNumber], [Timestamp]);
GO

IF OBJECT_ID('[dbo].[DispatchedEvents]') IS NULL
CREATE TABLE [dbo].[DispatchedEvents]
(
    [DispatcherId] UNIQUEIDENTIFIER NOT NULL,
    [SequenceNumber] BIGINT NOT NULL,
    CONSTRAINT [PK_DispatchedEvent] PRIMARY KEY CLUSTERED ([DispatcherId])
);
GO

CREATE OR ALTER PROCEDURE [dbo].[GetNextBatch]
    @DispatcherId UNIQUEIDENTIFIER,
    @MaxBatchSize INT,
    @BatchTimeoutMilliseconds INT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Lock INT;
DECLARE @First BIGINT;
DECLARE @Last BIGINT;
DECLARE @BatchId BIGINT;

BEGIN TRANSACTION;

    EXEC @Lock = sp_getapplock @Resource = @DispatcherId, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire the batch lock for the dispatcher.', 1;

    -- Batches that were never completed are handed out again after the timeout. The comparison is at millisecond
    -- precision: DATEDIFF(SECOND) counts second boundaries crossed, which would expire a batch taken at xx.999 at (xx+1).001.
    UPDATE [dbo].[Batches]
    SET [Complete] = 1
    WHERE [DispatcherId] = @DispatcherId
        AND [Complete] = 0
        AND [Timestamp] <= DATEADD(MILLISECOND, -@BatchTimeoutMilliseconds, SYSUTCDATETIME());

    DECLARE @After BIGINT = (
        SELECT MAX([SequenceNumber])
        FROM (
            SELECT COALESCE(MAX([LastSequenceNumber]), 0) AS [SequenceNumber] FROM [dbo].[Batches] WHERE [DispatcherId] = @DispatcherId AND [Complete] = 0
            UNION ALL
            SELECT COALESCE(MAX([SequenceNumber]), 0) FROM [dbo].[DispatchedEvents] WHERE [DispatcherId] = @DispatcherId
        ) AS [Positions]);

    -- The batch is bounded by actual events, so gaps left by rolled-back commits are skipped.
    SELECT @First = MIN([SequenceNumber]), @Last = MAX([SequenceNumber])
    FROM (
        SELECT TOP (@MaxBatchSize) [SequenceNumber]
        FROM [dbo].[Events]
        WHERE [SequenceNumber] > @After
        ORDER BY [SequenceNumber]) AS [Next];

    IF @First IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[Batches] ([DispatcherId], [SequenceNumber], [LastSequenceNumber])
        VALUES (@DispatcherId, @First, @Last);

        SET @BatchId = SCOPE_IDENTITY();
    END

COMMIT TRANSACTION;

SELECT @BatchId AS [BatchId]
WHERE @BatchId IS NOT NULL;

SELECT [Event].[SequenceNumber], [Type].[Name] AS [TypeName], [Event].[Payload]
FROM [dbo].[Events] [Event] INNER JOIN [dbo].[Types] [Type] ON [Event].[TypeId] = [Type].[Id]
WHERE @BatchId IS NOT NULL
    AND [Event].[SequenceNumber] BETWEEN @First AND @Last
ORDER BY [Event].[SequenceNumber];
GO

CREATE OR ALTER PROCEDURE [dbo].[MarkDispatched]
    @DispatcherId UNIQUEIDENTIFIER,
    @SequenceNumber BIGINT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

MERGE INTO [dbo].[DispatchedEvents] WITH (HOLDLOCK) AS [Target]
USING (SELECT @DispatcherId AS [DispatcherId], @SequenceNumber AS [SequenceNumber]) AS [Source]
ON [Target].[DispatcherId] = [Source].[DispatcherId]
WHEN MATCHED AND [Target].[SequenceNumber] < [Source].[SequenceNumber] THEN
    UPDATE SET [Target].[SequenceNumber] = [Source].[SequenceNumber]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([DispatcherId], [SequenceNumber])
    VALUES ([Source].[DispatcherId], [Source].[SequenceNumber]);

UPDATE [dbo].[Batches]
SET [Complete] = 1
WHERE [DispatcherId] = @DispatcherId
    AND [Complete] = 0
    AND [LastSequenceNumber] <= @SequenceNumber;
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Versions] WHERE [Version] = 1)
INSERT INTO [dbo].[Versions] ([Version]) VALUES (1);
GO
