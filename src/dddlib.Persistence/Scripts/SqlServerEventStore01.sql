CREATE TABLE [dbo].[Partitions]
(
    [Id] INT NOT NULL,
    [Key] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_Table] PRIMARY KEY ([Key])
);
GO

CREATE TABLE [dbo].[EventTypes]
(
    [PartitionKey] INT NOT NULL
);
GO

CREATE TABLE [dbo].[Events]
(
    [PartitionKey] INT NOT NULL,
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [StreamRevision] INT NOT NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [SequenceNumber] BIGINT NOT NULL,
    [EventTypeId] INT NOT NULL, -- this allows us to know what type to reconstitute
    [Payload] VARCHAR(MAX) NOT NULL,
    [Dispatched] BIT NOT NULL,
    CONSTRAINT [PK_Table] PRIMARY KEY ([SequenceNumber])
);
GO