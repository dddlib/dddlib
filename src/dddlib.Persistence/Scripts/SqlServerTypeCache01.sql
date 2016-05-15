-- NOTE (Cameron): This is all a bit hacky because of the move away from SqlServerPersistenceXX.sql.
IF OBJECT_ID('[dbo].[Types]') IS NULL
BEGIN
    CREATE TABLE [dbo].[Types]
    (
        [Id] [int] IDENTITY NOT NULL,
        [Name] [varchar](511) NOT NULL CHECK (DATALENGTH([Name]) > 0), -- LINK (Cameron): http://stackoverflow.com/questions/186523/what-is-the-maximum-length-of-a-c-cli-identifier
        CONSTRAINT [PK_Type] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Types]') AND name = N'IX_Type_Name')
    CREATE UNIQUE INDEX [IX_Type_Name] ON [dbo].[Types] ([Name]);
GO

CREATE PROCEDURE [dbo].[TryAddType]
    @Name VARCHAR(511)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

-- LINK (Cameron): http://stackoverflow.com/questions/3859085/force-t-sql-query-to-be-case-sensitive-in-ms
MERGE INTO [dbo].[Types] WITH (HOLDLOCK) AS [Target]
USING (SELECT @Name AS [Name]) AS [Source]
ON [Target].[Name] = [Source].[Name] COLLATE SQL_Latin1_General_CP1_CS_AS
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Name])
    VALUES ([Source].[Name]);

GO

CREATE PROCEDURE [dbo].[GetTypes]
AS
SELECT [Id], [Name]
FROM [dbo].[Types]
ORDER BY [Id];
GO