-- dddlib.Persistence: shared objects (the type registry).
-- Run this script first. All scripts are idempotent and target the [dbo] schema; replace [dbo] to use another schema.

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
