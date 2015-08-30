



CREATE PROCEDURE [dbo].[MarkEventAsDispatched]
    @EventId bigint
AS

SET NOCOUNT ON;

update dbo.[Events]
set Dispatched = 1
where Id = @EventId;

GO

-- query
select *
from dbo.[Events]

exec dbo.MonitorUndispatchedEvents



--exec dbo.GetExecutingBatch

--select Id, ROW_NUMBER() OVER (ORDER BY Id DESC) as [Rank], BatchId, Dispatched
--from dbo.[Events]
--where Dispatched = 0
--    and BatchId is not null
    


/*
-- reset all data to unprocessed
update dbo.[Events]
set BatchId = null, Processed = null, Dispatched = 0


-- SQL: ???
CREATE PROCEDURE [dbo].[MonitorUndispatchedBatches]
AS

SELECT [Id] as [EventId], [BatchId]
FROM [dbo].[Events]
WHERE [Dispatched] = 0
    AND [Processed] IS NOT NULL
ORDER BY [Id]

GO

CREATE PROCEDURE [dbo].[MonitorUndispatchedEvents]
AS

SELECT [Id] as [EventId]
FROM [dbo].[Events]
WHERE [Dispatched] = 0
    AND [Processed] IS NULL
ORDER BY [Id]

GO

CREATE PROCEDURE [dbo].[GetUndispatchedEvents]
    @BatchSize int
AS

declare @batchId uniqueidentifier = newid();

with potential as
(
    select top (@BatchSize) *, case when Processed is null then 1 else 0 end as [Priority]
    from dbo.[Events]
    where Processed is null
        or (Dispatched = 0 and Processed = 
            (
                select top 1 Processed
                from dbo.[Events]
                where Dispatched = 0
                    and Processed is not null
                    and datediff(ss, Processed, getdate()) >= 30 -- time in seconds to invalidate dispatch attempt
                order by Id, Processed
            ))
    order by Id
),
cte as
(
    select *
    from potential
    where [Priority] = (select min([Priority]) from potential)
)
update cte
set BatchId = @batchId, Processed = getdate()
output inserted.Id as [EventId], inserted.BatchId, inserted.Payload;

GO

*/