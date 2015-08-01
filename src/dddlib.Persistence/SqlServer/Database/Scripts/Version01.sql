-- NOTE (Cameron): The SQL comments are required for the alternate execution method for this script (via .NET)
IF EXISTS (SELECT * FROM information_schema.tables WHERE table_name='DatabaseVersion' AND table_schema = 'dbo') SET NOEXEC ON

-- LINK (Cameron): http://stackoverflow.com/questions/4443262/tsql-add-column-to-table-and-then-update-it-inside-transaction-go
SET XACT_ABORT ON
GO

BEGIN TRANSACTION
GO

-- SQL: Creating the 'database version' table
CREATE TABLE [dbo].[DatabaseVersion]
(
    [Id] [int] NOT NULL,
    [Description] [varchar](MAX) NOT NULL CHECK (DATALENGTH([Description]) > 0),
    [Timestamp] [datetime2] NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_DatabaseVersion] PRIMARY KEY CLUSTERED ([Id])
);
GO

IF XACT_STATE() < 1 SET NOEXEC ON
GO

-- SQL: Creating the 'aggregate root type' table
CREATE TABLE [dbo].[AggregateRootType]
(
    [Id] [int] IDENTITY NOT NULL,
    [Name] [varchar](511) NOT NULL CHECK (DATALENGTH([Name]) > 0), -- LINK (Cameron): http://stackoverflow.com/questions/186523/what-is-the-maximum-length-of-a-c-cli-identifier
    CONSTRAINT [PK_AggregateRootType] PRIMARY KEY CLUSTERED ([Id])
);
GO

IF XACT_STATE() < 1 SET NOEXEC ON
GO

-- SQL: Creating the 'natural key' table
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

IF XACT_STATE() < 1 SET NOEXEC ON
GO

-- SQL: Creating the 'get natural keys' stored procedure
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

IF XACT_STATE() < 1 SET NOEXEC ON
GO

-- SQL: Creating the 'try add natural key' stored procedure
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

IF XACT_STATE() < 1 SET NOEXEC ON
GO

SET NOCOUNT ON;

-- SQL: Assigning the database version as the initial version
INSERT INTO [dbo].[DatabaseVersion] ([Id], [Description])
SELECT 1, 'Initial version';
GO

SET NOEXEC OFF
GO

IF XACT_STATE() = 1 COMMIT
GO