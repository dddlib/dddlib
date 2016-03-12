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
    CONSTRAINT [FK_EventsTypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

CREATE PROCEDURE [dbo].[CommitStream]
    @StreamId UNIQUEIDENTIFIER,
    @PayloadTypeName VARCHAR(511),
    @Payload VARCHAR(MAX),
    @CorrelationId UNIQUEIDENTIFIER,
    @PreCommitState VARCHAR(36),
    @PostCommitState VARCHAR(36) = NULL OUTPUT
AS
SET NOCOUNT ON;

DECLARE @TypeId INT = (
    SELECT [Id]
    FROM [dbo].[Types]
    WHERE [Name] = @PayloadTypeName
);

IF @TypeId IS NULL
BEGIN
    INSERT INTO [dbo].[Types] ([Name])
    VALUES (@PayloadTypeName);
    SET @TypeId = SCOPE_IDENTITY();
END

DECLARE @Events TABLE ([SequenceNumber] BIGINT, [StreamRevision] INT, [State] VARCHAR(36));

IF EXISTS (
    SELECT [State]
    FROM [dbo].[Events]
    WHERE [State] = @PreCommitState
        AND [StreamId] = @StreamId
        AND [StreamRevision] = (
        SELECT MAX([StreamRevision])
        FROM [dbo].[Events]
        WHERE [StreamId] = @StreamId))
INSERT INTO [dbo].[Events] ([StreamId], [StreamRevision], [TypeId], [Payload], [CorrelationId], [SequenceNumber])
OUTPUT inserted.[SequenceNumber], inserted.[StreamRevision], inserted.[State] INTO @Events
SELECT
    @StreamId,
    (SELECT MAX([StreamRevision]) + 1 FROM [dbo].[Events] WHERE [StreamId] = @StreamId),
    @TypeId,
    @Payload,
    @CorrelationId,
    (SELECT MAX([SequenceNumber]) + 1 FROM [dbo].[Events]);

SELECT @PostCommitState = [State]
FROM @Events;

GO

CREATE PROCEDURE [dbo].[GetStream]
    @StreamId UNIQUEIDENTIFIER,
    @StreamRevision BIGINT
AS
SET NOCOUNT ON;

SELECT [Event].[StreamId], [Event].[StreamRevision], [Type].[Name] AS [PayloadTypeName], [Event].[Payload], [Event].[SequenceNumber], [Event].[State]
FROM [dbo].[Events] [Event] INNER JOIN [dbo].[Types] [Type] ON [Event].[TypeId] = [Type].[Id]
WHERE [Event].[StreamId] = @StreamId
ORDER BY [Event].[StreamRevision];

GO
