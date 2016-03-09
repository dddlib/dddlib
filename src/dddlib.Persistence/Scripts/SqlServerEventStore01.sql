CREATE TABLE [dbo].[Events]
(
    [StreamId] UNIQUEIDENTIFIER NOT NULL,
    [StreamRevision] INT NOT NULL,
    [TypeId] INT NOT NULL, -- this allows us to know which type to reconstitute
    [Payload] VARCHAR(MAX) NOT NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [SequenceNumber] BIGINT NOT NULL,
    CONSTRAINT [PK_Event] PRIMARY KEY ([SequenceNumber]),
    CONSTRAINT [FK_TypeId_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [dbo].[Types] ([Id])
);
GO

--CREATE PROCEDURE [dbo].[AddEvent]

--CREATE PROCEDURE [dbo].[GetEvents]
