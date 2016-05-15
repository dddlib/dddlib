
-- test #1
DECLARE
    @StreamId UNIQUEIDENTIFIER = newid(),
    @Events [dbo].[Events],
    @CorrelationId UNIQUEIDENTIFIER = newid(),
    @PreCommitState VARCHAR(MAX),
    @PostCommitState VARCHAR(MAX),
    @Counter INT = 0;

-- add events
WHILE @Counter < 1
BEGIN
    INSERT INTO @Events
    SELECT @Counter, 1, CONVERT(VARBINARY(MAX), 'metadata'), CONVERT(VARBINARY(MAX), 'event');
    SET @Counter = @Counter + 1;  -- increment counter
END

--SET @PreCommitState = '5DA8155E';
SET @Counter = 0;

WHILE @Counter < 100000
BEGIN
    EXEC [dbo].[CommitStream] @StreamId, @Events, @CorrelationId, @PreCommitState, @PostCommitState OUTPUT;
    SET @PreCommitState = @PostCommitState;
    SET @Counter = @Counter + 1;  -- increment counter
END

-- test #2
DECLARE
    @StreamId UNIQUEIDENTIFIER = newid(),
    @Events [dbo].[Events],
    @CorrelationId UNIQUEIDENTIFIER = newid(),
    @PreCommitState VARCHAR(MAX),
    @PostCommitState VARCHAR(MAX),
    @Counter INT = 0;

-- add events
WHILE @Counter < 1000
BEGIN
    INSERT INTO @Events
    SELECT @Counter, 1, CONVERT(VARBINARY(MAX), 'metadata'), CONVERT(VARBINARY(MAX), 'event');
    SET @Counter = @Counter + 1;  -- increment counter
END

--SET @PreCommitState = '5DA8155E';
SET @Counter = 0;

WHILE @Counter < 100
BEGIN
    EXEC [dbo].[CommitStream] @StreamId, @Events, @CorrelationId, @PreCommitState, @PostCommitState OUTPUT;
    SET @PreCommitState = @PostCommitState;
    SET @Counter = @Counter + 1;  -- increment counter
END

-- test #3
DECLARE
    @StreamId UNIQUEIDENTIFIER = newid(),
    @Events [dbo].[Events],
    @CorrelationId UNIQUEIDENTIFIER = newid(),
    @PreCommitState VARCHAR(MAX),
    @PostCommitState VARCHAR(MAX),
    @Counter INT = 0;

-- add events
WHILE @Counter < 1
BEGIN
    INSERT INTO @Events
    SELECT @Counter, 1, CONVERT(VARBINARY(MAX), 'metadata'), CONVERT(VARBINARY(MAX), 'event');
    SET @Counter = @Counter + 1;  -- increment counter
END

SET @Counter = 0;

WHILE @Counter < 100000
BEGIN
    SET @StreamId = newid();
    SET @CorrelationId = newid();
    EXEC [dbo].[CommitStream] @StreamId, @Events, @CorrelationId, @PreCommitState, @PostCommitState OUTPUT;
    --SET @PreCommitState = @PostCommitState;
    SET @Counter = @Counter + 1;  -- increment counter
END

-- test #4
SET STATISTICS IO ON 
SET STATISTICS TIME ON 
GO

DECLARE	@State VARCHAR(36);
exec dbo.GetStream '2CC01AA7-4D0F-41C2-81EA-7813843F360F', 0, @State OUTPUT;
print @State
GO

SET STATISTICS IO OFF 
SET STATISTICS TIME OFF 
GO


/*

-- clean up
truncate table [dbo].[Events];
delete from [dbo].[Streams];

-- view
select * from [dbo].[Types]
select * from [dbo].[Streams] where Revision > 1
select * from [dbo].[Events] where StreamLinkId = 3

-- example
DECLARE	@State VARCHAR(36);
exec dbo.GetStream '4D989FDF-C31C-4E33-A887-04AD61FBF489', 1, @State OUTPUT;
print '"' + @State + '"';

*/

