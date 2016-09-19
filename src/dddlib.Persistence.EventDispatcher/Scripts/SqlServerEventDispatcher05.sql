ALTER PROCEDURE [dbo].[GetNextUndispatchedEventsBatch]
    @DispatcherId UNIQUEIDENTIFIER,
    @MaxBatchSize INT
AS

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ARITHABORT ON;

-- NOTE (Cameron): This is the temporal bit of this stored procedure where we discard any incomplete (overdue) batches.
UPDATE [dbo].[Batches]
SET [Complete] = 1
WHERE [DispatcherId] = @DispatcherId
    AND [Complete] = 0
    AND [SequenceNumber] >= (
        SELECT MIN([SequenceNumber])
        FROM [dbo].[Batches]
        WHERE [DispatcherId] = @DispatcherId
            AND [Complete] = 0
            AND DATEDIFF(ss, [Timestamp], GETDATE()) >= 30);

DECLARE @Lock INT;
DECLARE @MaxSequenceNumber BIGINT;
DECLARE @DispatchedSequenceNumber BIGINT;
DECLARE @BatchedSequenceNumber BIGINT;

BEGIN TRANSACTION

    -- LINK (Cameron): http://rusanu.com/2015/03/06/the-cost-of-a-transactions-that-has-only-applocks/
    EXEC @Lock = [tempdb]..[sp_getapplock] @Resource = @DispatcherId, @LockMode = 'Exclusive', @LockTimeout = 1000;
    IF @Lock < 0
        THROW 50500, 'Concurrency error (server side). Failed to acquire commit lock for dispatcher.', 1;

    SELECT @MaxSequenceNumber = MAX([SequenceNumber])
    FROM [dbo].[Events];

    SELECT @DispatchedSequenceNumber = MAX([SequenceNumber])
    FROM [dbo].[DispatchedEvents]
    WHERE [DispatcherId] = @DispatcherId;

    SELECT @BatchedSequenceNumber = COALESCE(MAX([SequenceNumber] + [Size] - 1), @DispatchedSequenceNumber, 0)
    FROM [dbo].[Batches]
    WHERE [DispatcherId] = @DispatcherId
        AND [Complete] = 0;

    DECLARE @Size INT = IIF(@MaxSequenceNumber - @BatchedSequenceNumber >= @MaxBatchSize, @MaxBatchSize, @MaxSequenceNumber - @BatchedSequenceNumber);

    INSERT INTO [dbo].[Batches] ([DispatcherId], [SequenceNumber], [Size])
    SELECT @DispatcherId, @BatchedSequenceNumber + 1, @Size
    WHERE @Size > 0;

COMMIT TRANSACTION

DECLARE @BatchId BIGINT = SCOPE_IDENTITY();

SELECT @BatchId AS [BatchId]
WHERE @BatchId IS NOT NULL;

SELECT [Event].[SequenceNumber], [Type].[Name] AS [PayloadTypeName], [Event].[Payload]
FROM [dbo].[Events] [Event]
    INNER JOIN [dbo].[Batches] [Batch] ON ([Event].[SequenceNumber] >= [Batch].[SequenceNumber] AND [Event].[SequenceNumber] < [Batch].[SequenceNumber] + [Batch].[Size])
    INNER JOIN [dbo].[Types] [Type] ON [Event].[TypeId] = [Type].[Id]
WHERE [Batch].[Id] = @BatchId;

GO