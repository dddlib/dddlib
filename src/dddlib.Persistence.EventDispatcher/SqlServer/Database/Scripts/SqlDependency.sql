--SELECT NAME, IS_BROKER_ENABLED FROM SYS.DATABASES

CREATE DATABASE SqlDependency
GO
ALTER DATABASE SqlDependency SET ENABLE_BROKER
GO

USE SqlDependency
GO

CREATE TABLE [dbo].[Stuff]
(
    [Id] INT IDENTITY NOT NULL,
    [Code]       VARCHAR (50) NOT NULL,
    [LastUpdate] DATETIME     DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id])
);
GO

INSERT INTO [dbo].[Stuff] ([Code])
SELECT 'Something'
UNION SELECT 'Something else';
GO