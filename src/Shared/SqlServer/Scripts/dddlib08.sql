-- dddlib SQL Server schema, version 8: the event feed reads each event's metadata.
--
-- Applied by SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer), SqlServerEventDispatcherSchema.EnsureAsync
-- (dddlib.Persistence.EventDispatcher.SqlServer) or SqlServerProjectionsSchema.EnsureAsync
-- (dddlib.Persistence.Projections.SqlServer). ReadEvents returns the events' Metadata as a seventh column, after the
-- six that feeds requiring version 2 to 7 read by position; its parameters and the rest of its body are as in version
-- 2, and no table changes. Idempotent like the earlier versions.
--
-- Metadata holds dddlib's hostname and timestamp and any entries the repository that saved the event added, such as a
-- tenant, so that a projection can read who saved an event and where (https://github.com/dddlib/dddlib/issues/60).

-- Event feed.

-- As in version 2, with the events' metadata.
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

SELECT [Event].[SequenceNumber], [Stream].[Id] AS [StreamId], [Event].[StreamRevision], [Event].[CorrelationId], [Type].[Name] AS [TypeName], [Event].[Payload], [Event].[Metadata]
FROM @Page [Page]
    INNER JOIN [dbo].[Events] [Event] ON [Event].[SequenceNumber] = [Page].[SequenceNumber]
    INNER JOIN [dbo].[Streams] [Stream] ON [Stream].[LinkId] = [Event].[StreamLinkId]
    INNER JOIN [dbo].[Types] [Type] ON [Type].[Id] = [Event].[TypeId]
WHERE NOT EXISTS (SELECT 1 FROM @TypeNames)
    OR EXISTS (SELECT 1 FROM @TypeNames [Wanted] WHERE [Wanted].[Name] = [Type].[Name] COLLATE SQL_Latin1_General_CP1_CS_AS)
ORDER BY [Event].[SequenceNumber];
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Versions] WHERE [Version] = 8)
INSERT INTO [dbo].[Versions] ([Version]) VALUES (8);
GO
