exec dbo.MonitorUndispatchedEvents

exec dbo.MonitorUndispatchedBatches

INSERT INTO [dbo].[Events] ([Payload])
VALUES ('event1'), ('event2'), ('event3'), ('event4'), ('event5'), ('event6'), ('event7'), ('event8'), ('event9'), ('event10'), ('event11')


BEGIN TRANSACTION;

INSERT INTO [dbo].[Events] ([Payload])
VALUES ('test1'), ('test2'), ('test3'), ('test4'), ('test5'), ('test6'), ('test7'), ('test8'), ('test9'), ('test10'), ('test11'), ('test12')

COMMIT;


SELECT *
FROM [dbo].[Batches]
WHERE [Complete] = 0
    AND DATEDIFF(ss, [Timestamp], GETDATE()) >= 30; -- time in seconds to invalidate dispatch attempt


SELECT *
--SET [Event].[BatchId] = NULL
FROM [dbo].[Events] [Event] INNER JOIN [dbo].[Batches] [Batch] ON [Event].[BatchId] = [Batch].[Id]
WHERE [Batch].[Complete] = 1
    AND [Event].[Dispatched] = 0;


truncate table [dbo].[Events]
truncate table [dbo].[Batches]