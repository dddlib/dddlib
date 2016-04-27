CREATE TABLE [dbo].[Types]
(
    [Id] [int] IDENTITY NOT NULL,
    [Name] [varchar](511) NOT NULL CHECK (DATALENGTH([Name]) > 0), -- LINK (Cameron): http://stackoverflow.com/questions/186523/what-is-the-maximum-length-of-a-c-cli-identifier
    CONSTRAINT [PK_Type] PRIMARY KEY CLUSTERED ([Id])
);
GO

CREATE UNIQUE INDEX [IX_Type_Name] ON [dbo].[Types] ([Name]);
GO