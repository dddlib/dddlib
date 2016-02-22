CREATE TABLE [dbo].[Snapshots]
(
    --[PartitionId] INT NOT NULL,
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [StreamRevision] INT NOT NULL,
    [Payload] VARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_Table] PRIMARY KEY ([StreamId])
);
GO