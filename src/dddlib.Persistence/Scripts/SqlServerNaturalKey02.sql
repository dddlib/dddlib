ALTER TABLE [dbo].[NaturalKeys]
ADD [IsRemoved] [bit] NOT NULL DEFAULT 0;
GO

ALTER PROCEDURE [dbo].[GetNaturalKeys]
    @AggregateRootTypeName VARCHAR(511),
    @Checkpoint BIGINT
AS
SET NOCOUNT ON;

SELECT [NaturalKey].[Id], [NaturalKey].[SerializedValue], [NaturalKey].[IsRemoved], [NaturalKey].[Checkpoint], [NaturalKey].[IsRemoved]
FROM [dbo].[NaturalKeys] [NaturalKey] INNER JOIN [dbo].[Types] [Type] ON [NaturalKey].[TypeId] = [Type].[Id]
WHERE [NaturalKey].[Checkpoint] > @Checkpoint AND [Type].[Name] = @AggregateRootTypeName
ORDER BY [NaturalKey].[Checkpoint];

GO

CREATE PROCEDURE [dbo].[RemoveNaturalKey]
    @Id UNIQUEIDENTIFIER
AS
SET NOCOUNT ON;

DECLARE @TypeId INT = (
    SELECT [Type].[Id]
    FROM [dbo].[Types] [Type] INNER JOIN [dbo].[NaturalKeys] [NaturalKey] ON [Type].[Id] = [NaturalKey].[TypeId]
    WHERE [NaturalKey].[Id] = @Id
);

UPDATE [dbo].[NaturalKeys]
SET [IsRemoved] = 1, [Checkpoint] = (
    SELECT COALESCE(MAX([Checkpoint]), 0) + CONVERT(BIGINT, 1)
    FROM [dbo].[NaturalKeys]
    WHERE [TypeId] = @TypeId
    )
WHERE [Id] = @Id AND [IsRemoved] = 0;

GO