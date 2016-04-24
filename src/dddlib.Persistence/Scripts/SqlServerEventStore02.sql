CREATE PROCEDURE [dbo].[GetEventsFrom]
    @SequenceNumber BIGINT
AS

SELECT [Event].[SequenceNumber], [Type].[Name] AS [PayloadTypeName], [Event].[Payload]
FROM [dbo].[Events] [Event] WITH (NOLOCK)INNER JOIN [dbo].[Types] [Type] ON [Event].[TypeId] = [Type].[Id]
WHERE [Event].[SequenceNumber] > @SequenceNumber
ORDER BY [Event].[SequenceNumber];

GO