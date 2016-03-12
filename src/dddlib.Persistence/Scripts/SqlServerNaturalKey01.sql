CREATE TABLE [dbo].[NaturalKeys]
(
    [Id] [uniqueidentifier] NOT NULL CHECK ([Id] != 0x0) DEFAULT NEWSEQUENTIALID(),
    [TypeId] [int] NOT NULL,
    [Checkpoint] [bigint] NOT NULL,
    [SerializedValue] [varchar](MAX) NOT NULL CHECK (DATALENGTH([SerializedValue]) > 0),
    CONSTRAINT [PK_NaturalKeys] PRIMARY KEY CLUSTERED ([TypeId], [Checkpoint]),
    CONSTRAINT [FK_TypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

CREATE PROCEDURE [dbo].[GetNaturalKeys]
    @AggregateRootTypeName VARCHAR(511),
    @Checkpoint BIGINT
AS
SET NOCOUNT ON;

SELECT [NaturalKey].[Id], [NaturalKey].[SerializedValue], [NaturalKey].[Checkpoint]
FROM [dbo].[NaturalKeys] [NaturalKey] INNER JOIN [dbo].[Types] [Type] ON [NaturalKey].[TypeId] = [Type].[Id]
WHERE [NaturalKey].[Checkpoint] > @Checkpoint AND [Type].[Name] = @AggregateRootTypeName
ORDER BY [NaturalKey].[Checkpoint];

GO

CREATE PROCEDURE [dbo].[TryAddNaturalKey]
    @AggregateRootTypeName VARCHAR(511),
    @SerializedValue VARCHAR(MAX),
    @Checkpoint BIGINT
AS
SET NOCOUNT ON;

DECLARE @TypeId INT = (
    SELECT [Id]
    FROM [dbo].[Types]
    WHERE [Name] = @AggregateRootTypeName
);

IF @TypeId IS NULL
BEGIN
    INSERT INTO [dbo].[Types] ([Name])
    VALUES (@AggregateRootTypeName);
    SET @TypeId = SCOPE_IDENTITY();
END

DECLARE @NaturalKeys TABLE ([Id] uniqueidentifier, [Checkpoint] bigint);

IF ((SELECT ISNULL(MAX([Checkpoint]), 0) AS [Checkpoint] FROM [dbo].[NaturalKeys] WHERE [TypeId] = @TypeId) = @Checkpoint)
INSERT INTO [dbo].[NaturalKeys] ([TypeId], [Checkpoint], [SerializedValue])
OUTPUT inserted.[Id], inserted.[Checkpoint] INTO @NaturalKeys
SELECT @TypeId, @Checkpoint + CONVERT(BIGINT, 1), @SerializedValue;

SELECT *
FROM @NaturalKeys;

GO
