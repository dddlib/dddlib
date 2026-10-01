-- dddlib SQL Server schema, version 2: the event feed and projections.
--
-- Applied by SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer), SqlServerEventDispatcherSchema.EnsureAsync
-- (dddlib.Persistence.EventDispatcher.SqlServer) or SqlServerProjectionsSchema.EnsureAsync
-- (dddlib.Persistence.Projections.SqlServer). Expand only: nothing that version 1 created is changed or removed, so
-- packages that require version 1 keep working. Idempotent like version 1, so the whole series can be run by hand
-- more than once.

-- Event feed.

IF TYPE_ID('[dbo].[TypeNameList]') IS NULL
CREATE TYPE [dbo].[TypeNameList] AS TABLE
(
    [Name] VARCHAR(511) NOT NULL PRIMARY KEY CLUSTERED
);
GO

-- The next @MaxCount committed events after @AfterSequenceNumber, in sequence order. The first result is the sequence
-- number the page extends to (NULL when there are no more events); the second is the page's events, or only those of
-- the named types when @TypeNames is not empty, so that a reader's checkpoint advances past the events it does not
-- want without their payloads being read.
--
-- Commits are serialized on the dddlib.Events.Commit lock, so the in-flight commit, if any, holds the highest
-- sequence numbers: a page that reaches it waits for it (or, under read committed snapshot, stops before it) and
-- never passes an event that later appears below its end.
CREATE OR ALTER PROCEDURE [dbo].[ReadEvents]
    @AfterSequenceNumber BIGINT,
    @MaxCount INT,
    @TypeNames [dbo].[TypeNameList] READONLY
AS
SET NOCOUNT ON;

DECLARE @Page TABLE ([SequenceNumber] BIGINT NOT NULL PRIMARY KEY, [TypeId] INT NOT NULL);

INSERT INTO @Page ([SequenceNumber], [TypeId])
SELECT TOP (@MaxCount) [SequenceNumber], [TypeId]
FROM [dbo].[Events]
WHERE [SequenceNumber] > @AfterSequenceNumber
ORDER BY [SequenceNumber];

SELECT MAX([SequenceNumber]) AS [EndSequenceNumber]
FROM @Page;

SELECT [Event].[SequenceNumber], [Stream].[Id] AS [StreamId], [Event].[StreamRevision], [Event].[CorrelationId], [Type].[Name] AS [TypeName], [Event].[Payload]
FROM @Page [Page]
    INNER JOIN [dbo].[Events] [Event] ON [Event].[SequenceNumber] = [Page].[SequenceNumber]
    INNER JOIN [dbo].[Streams] [Stream] ON [Stream].[LinkId] = [Event].[StreamLinkId]
    INNER JOIN [dbo].[Types] [Type] ON [Type].[Id] = [Event].[TypeId]
WHERE NOT EXISTS (SELECT 1 FROM @TypeNames)
    OR EXISTS (SELECT 1 FROM @TypeNames [Wanted] WHERE [Wanted].[Name] = [Type].[Name] COLLATE SQL_Latin1_General_CP1_CS_AS)
ORDER BY [Event].[SequenceNumber];
GO

CREATE OR ALTER PROCEDURE [dbo].[GetLastSequenceNumber]
AS
SET NOCOUNT ON;

SELECT COALESCE(MAX([SequenceNumber]), 0) AS [LastSequenceNumber]
FROM [dbo].[Events];
GO

-- Projections: one row per projection name with its checkpoint, and the views of the key/value kind. Names and keys
-- are binary-collated so equality is ordinal, like the default comparers, rather than the database collation. The
-- views key on the projection's integer id: with the name, the clustered key would exceed 900 bytes.

IF OBJECT_ID('[dbo].[Projections]') IS NULL
CREATE TABLE [dbo].[Projections]
(
    [Id] INT IDENTITY NOT NULL,
    [Name] VARCHAR(511) COLLATE Latin1_General_100_BIN2 NOT NULL CHECK (DATALENGTH([Name]) > 0),
    [Checkpoint] BIGINT NOT NULL CONSTRAINT [DF_Projection_Checkpoint] DEFAULT 0,
    CONSTRAINT [PK_Projection] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UX_Projection_Name] UNIQUE ([Name])
);
GO

IF OBJECT_ID('[dbo].[ProjectionViews]') IS NULL
CREATE TABLE [dbo].[ProjectionViews]
(
    [ProjectionId] INT NOT NULL,
    [Key] NVARCHAR(400) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_ProjectionView] PRIMARY KEY CLUSTERED ([ProjectionId], [Key]),
    CONSTRAINT [FK_ProjectionViewProjectionId_ProjectionId] FOREIGN KEY ([ProjectionId]) REFERENCES [dbo].[Projections] ([Id])
);
GO

IF TYPE_ID('[dbo].[ProjectionViewList]') IS NULL
CREATE TYPE [dbo].[ProjectionViewList] AS TABLE
(
    [Key] NVARCHAR(400) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY CLUSTERED,
    [Payload] NVARCHAR(MAX) NULL
);
GO

-- Registers a projection name atomically: two concurrent callers must not both insert it.
CREATE OR ALTER PROCEDURE [dbo].[TryAddProjection]
    @Name VARCHAR(511)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

MERGE INTO [dbo].[Projections] WITH (HOLDLOCK) AS [Target]
USING (SELECT @Name AS [Name]) AS [Source]
ON [Target].[Name] = [Source].[Name]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Name])
    VALUES ([Source].[Name]);
GO

CREATE OR ALTER PROCEDURE [dbo].[GetProjectionCheckpoint]
    @Name VARCHAR(511)
AS
SET NOCOUNT ON;

SELECT COALESCE((SELECT [Checkpoint] FROM [dbo].[Projections] WHERE [Name] = @Name), 0) AS [Checkpoint];
GO

-- Moves the checkpoint from @ExpectedCheckpoint to @Checkpoint, registering the projection at checkpoint 0 when it is
-- new, and raises 50409 when the stored checkpoint is not the expected one. Call it first in the transaction that
-- writes the batch: the row stays locked until that transaction ends, so a second runner of the same projection waits
-- here and then fails, before it does any work.
CREATE OR ALTER PROCEDURE [dbo].[AdvanceProjectionCheckpoint]
    @Name VARCHAR(511),
    @ExpectedCheckpoint BIGINT,
    @Checkpoint BIGINT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

EXEC [dbo].[TryAddProjection] @Name = @Name;

UPDATE [dbo].[Projections]
SET [Checkpoint] = @Checkpoint
WHERE [Name] = @Name AND [Checkpoint] = @ExpectedCheckpoint;

IF @@ROWCOUNT = 0
    THROW 50409, 'Concurrency error (client side). Projection checkpoint mismatch.', 1;
GO

CREATE OR ALTER PROCEDURE [dbo].[ResetProjectionCheckpoint]
    @Name VARCHAR(511)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

UPDATE [dbo].[Projections]
SET [Checkpoint] = 0
WHERE [Name] = @Name;
GO

CREATE OR ALTER PROCEDURE [dbo].[GetProjectionView]
    @Name VARCHAR(511),
    @Key NVARCHAR(400)
AS
SET NOCOUNT ON;

SELECT [View].[Payload]
FROM [dbo].[ProjectionViews] [View] INNER JOIN [dbo].[Projections] [Projection] ON [View].[ProjectionId] = [Projection].[Id]
WHERE [Projection].[Name] = @Name AND [View].[Key] = @Key;
GO

CREATE OR ALTER PROCEDURE [dbo].[GetProjectionViews]
    @Name VARCHAR(511)
AS
SET NOCOUNT ON;

SELECT [View].[Key], [View].[Payload]
FROM [dbo].[ProjectionViews] [View] INNER JOIN [dbo].[Projections] [Projection] ON [View].[ProjectionId] = [Projection].[Id]
WHERE [Projection].[Name] = @Name
ORDER BY [View].[Key];
GO

-- Upserts the views with a payload and removes the ones without, in one statement.
CREATE OR ALTER PROCEDURE [dbo].[SaveProjectionViews]
    @Name VARCHAR(511),
    @Views [dbo].[ProjectionViewList] READONLY
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

    EXEC [dbo].[TryAddProjection] @Name = @Name;

    DECLARE @ProjectionId INT = (SELECT [Id] FROM [dbo].[Projections] WHERE [Name] = @Name);

    MERGE INTO [dbo].[ProjectionViews] WITH (HOLDLOCK) AS [Target]
    USING (SELECT @ProjectionId AS [ProjectionId], [Key], [Payload] FROM @Views) AS [Source]
    ON [Target].[ProjectionId] = [Source].[ProjectionId] AND [Target].[Key] = [Source].[Key]
    WHEN MATCHED AND [Source].[Payload] IS NULL THEN
        DELETE
    WHEN MATCHED THEN
        UPDATE SET [Target].[Payload] = [Source].[Payload]
    WHEN NOT MATCHED BY TARGET AND [Source].[Payload] IS NOT NULL THEN
        INSERT ([ProjectionId], [Key], [Payload])
        VALUES ([Source].[ProjectionId], [Source].[Key], [Source].[Payload]);

COMMIT TRANSACTION;
GO

CREATE OR ALTER PROCEDURE [dbo].[DeleteProjectionViews]
    @Name VARCHAR(511)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DELETE [View]
FROM [dbo].[ProjectionViews] [View] INNER JOIN [dbo].[Projections] [Projection] ON [View].[ProjectionId] = [Projection].[Id]
WHERE [Projection].[Name] = @Name;
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Versions] WHERE [Version] = 2)
INSERT INTO [dbo].[Versions] ([Version]) VALUES (2);
GO
