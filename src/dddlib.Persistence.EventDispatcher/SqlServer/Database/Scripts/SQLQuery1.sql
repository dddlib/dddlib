exec [dbo].[MonitorUndispatchedEvents]
exec [dbo].[MonitorUndispatchedBatches]
exec [dbo].[GetNextUndispatchedEventsBatch] 5


truncate table [dbo].[Batches];
truncate table [dbo].[Events];

-- bulk add events
DECLARE @EventNumber INT = 1;
WHILE @EventNumber <= 777
BEGIN 
    INSERT INTO [dbo].[Events] ([Payload])
    VALUES('event' + LTRIM(STR(@EventNumber))); 
    SET @EventNumber = @EventNumber + 1;
END


