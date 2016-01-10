IF NOT EXISTS (SELECT * FROM information_schema.schemata WHERE schema_name = 'dbo')
    EXEC sp_executesql N'CREATE SCHEMA [dbo];';
GO

CREATE TABLE [dbo].[AggregateRootType]
(
    [Id] [int] IDENTITY NOT NULL,
    [Name] [varchar](511) NOT NULL CHECK (DATALENGTH([Name]) > 0), -- LINK (Cameron): http://stackoverflow.com/questions/186523/what-is-the-maximum-length-of-a-c-cli-identifier
    CONSTRAINT [PK_AggregateRootType] PRIMARY KEY CLUSTERED ([Id])
);
GO

CREATE TABLE [dbo].[NaturalKey]
(
    [Id] [uniqueidentifier] NOT NULL CHECK ([Id] != 0x0) DEFAULT NEWSEQUENTIALID(),
    [AggregateRootTypeId] [int] NOT NULL,
    [Checkpoint] [bigint] NOT NULL,
    [SerializedValue] [varchar](MAX) NOT NULL CHECK (DATALENGTH([SerializedValue]) > 0),
    CONSTRAINT [PK_NaturalKey] PRIMARY KEY CLUSTERED ([AggregateRootTypeId], [Checkpoint]),
    CONSTRAINT [FK_NaturalKey_AggregateRootTypeId] FOREIGN KEY ([AggregateRootTypeId]) REFERENCES [dbo].[AggregateRootType] ([Id])
);
GO

CREATE PROCEDURE [dbo].[GetNaturalKeys]
    @AggregateRootTypeName varchar(511),
    @Checkpoint bigint
AS
SET NOCOUNT ON;

SELECT [dbo].[NaturalKey].[Id], [dbo].[NaturalKey].[SerializedValue], [dbo].[NaturalKey].[Checkpoint]
FROM [dbo].[NaturalKey] INNER JOIN [dbo].[AggregateRootType] on [dbo].[NaturalKey].[AggregateRootTypeId] = [dbo].[AggregateRootType].[Id]
WHERE [dbo].[NaturalKey].[Checkpoint] > @Checkpoint AND [dbo].[AggregateRootType].[Name] = @AggregateRootTypeName
ORDER BY [dbo].[NaturalKey].[Checkpoint];

GO

CREATE PROCEDURE [dbo].[TryAddNaturalKey]
    @AggregateRootTypeName varchar(511),
    @SerializedValue varchar(MAX),
    @Checkpoint bigint
AS
SET NOCOUNT ON;

IF NOT EXISTS (SELECT * FROM [dbo].[AggregateRootType] WHERE [Name] = @AggregateRootTypeName)
INSERT INTO [dbo].[AggregateRootType] ([Name])
SELECT @AggregateRootTypeName;

DECLARE @AggregateRootTypeId int;
SELECT @AggregateRootTypeId = [Id]
FROM [dbo].[AggregateRootType]
WHERE [Name] = @AggregateRootTypeName;

DECLARE @NaturalKey TABLE ([Id] uniqueidentifier, [Checkpoint] bigint);

IF ((SELECT ISNULL(MAX([Checkpoint]), 0) AS [Checkpoint] FROM [dbo].[NaturalKey] WHERE [AggregateRootTypeId] = @AggregateRootTypeId) = @Checkpoint)
INSERT INTO [dbo].[NaturalKey] ([AggregateRootTypeId], [Checkpoint], [SerializedValue])
OUTPUT inserted.[Id], inserted.[Checkpoint] INTO @NaturalKey
SELECT @AggregateRootTypeId, @Checkpoint + CONVERT(bigint, 1), @SerializedValue;

SELECT *
FROM @NaturalKey;

GO
