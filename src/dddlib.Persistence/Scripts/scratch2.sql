-- test #1
DECLARE
    @StreamId UNIQUEIDENTIFIER = newid(),
    @Events [dddlib].[Events],
    @TypeId INT = 1,
    @Metadata VARCHAR(MAX) = 'metadata',
    @Payload VARCHAR(MAX) = 'event',
    @CorrelationId UNIQUEIDENTIFIER = newid(),
    @PreCommitState VARCHAR(MAX),
    @PostCommitState VARCHAR(MAX),
    @Counter INT = 0;

---- add events
--WHILE @Counter < 10
--BEGIN
--    INSERT INTO @Events
--    SELECT @Counter, 1, @Payload;
--    SET @Counter = @Counter + 1;  -- increment counter
--END

--SET @Counter = 0;

--WHILE @Counter < 10000
--BEGIN
--    EXEC [dddlib].[CommitStream] @StreamId, @Events, @Metadata, @CorrelationId, @PreCommitState, @PostCommitState OUTPUT;
--    SET @PreCommitState = @PostCommitState;
--    SET @Counter = @Counter + 1;  -- increment counter
--END



EXEC [dddlib].[CommitStream2] @StreamId, @TypeId, @Metadata, @Payload, @CorrelationId, @PreCommitState, @PostCommitState OUTPUT;
--SET @PreCommitState = @PostCommitState;


select *
from dddlib.Streams


DBCC DROPCLEANBUFFERS
DBCC FREEPROCCACHE
SET STATISTICS IO ON
SET STATISTICS TIME ON

DECLARE @State VARCHAR(MAX);
exec dddlib.GetStream '5A13BAA3-9C99-42F7-BB88-36E30983947C', 0, @State OUTPUT;
PRINT @State

SET STATISTICS IO OFF
SET STATISTICS TIME OFF


/*
1st run:
SQL Server parse and compile time: 
   CPU time = 687 ms, elapsed time = 836 ms.
   Table 'Streams'. Scan count 0, logical reads 2, physical reads 0, read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.
   Table 'Events'. Scan count 1, logical reads 308410, physical reads 261, read-ahead reads 214, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.
 SQL Server Execution Times:
   CPU time = 406 ms,  elapsed time = 1110 ms.
 SQL Server Execution Times:
   CPU time = 1093 ms,  elapsed time = 1949 ms.

2nd run:
SQL Server parse and compile time: 
   CPU time = 0 ms, elapsed time = 0 ms.
Table 'Streams'. Scan count 0, logical reads 2, physical reads 0, read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.
Table 'Events'. Scan count 1, logical reads 306576, physical reads 0, read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob read-ahead reads 0.
 SQL Server Execution Times:
   CPU time = 375 ms,  elapsed time = 1252 ms.
 SQL Server Execution Times:
   CPU time = 375 ms,  elapsed time = 1253 ms.
*/

SELECT  p.usecounts, size_in_bytes, cacheobjtype, objtype, t.[text] 
FROM   sys.dm_exec_cached_plans p 
CROSS APPLY sys.dm_exec_sql_text(p.plan_handle) t
WHERE   t.[text] LIKE '%dddlib.GetStream%'