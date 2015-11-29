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

-- SQL: Creating the 'event' table
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