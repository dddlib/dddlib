IF NOT EXISTS (SELECT * FROM information_schema.schemata WHERE schema_name = 'dbo')
    EXEC sp_executesql N'CREATE SCHEMA [dbo];';
GO

CREATE TABLE [dbo].[DatabaseVersion]
(
    [Id] [int] NOT NULL,
    [Description] [varchar](MAX) NOT NULL CHECK (DATALENGTH([Description]) > 0),
    [Timestamp] [datetime2] NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_DatabaseVersion] PRIMARY KEY CLUSTERED ([Id])
);
GO

CREATE TABLE [dbo].[Event]
(
    [Id] BIGINT NOT NULL,
    [CommitId] UNIQUEIDENTIFIER NOT NULL, 
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [EventTypeId] INT NOT NULL, 
    [Payload] VARCHAR(MAX) NOT NULL, 
    [Dispatched] BIT NOT NULL, 
    CONSTRAINT [PK_Table] PRIMARY KEY ([Id])
);
GO