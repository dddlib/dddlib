using System.Collections.Concurrent;
using System.Globalization;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.Tests.Integration;

// Every add and removal of a natural key writes at the next checkpoint of its type. Writers that did not take turns
// read the same latest checkpoint, collided on the primary key and retried: under 50 writers thousands of collisions in
// 1000 operations, with the unluckiest removals waiting 10 to 20 seconds and timing out under load. How long the
// writers wait in turn depends on the machine; how often they collide does not, so that is what this class measures.
// It has a database of its own, so that the collisions it counts are its own.
public class SqlServerNaturalKeyContentionTests : SqlServerIntegration
{
    [Test]
    public async Task ConcurrentWritersOfATypeTakeTurnsAtItsCheckpoints()
    {
        var session = string.Concat("dddlib_", Guid.NewGuid().ToString("N"));
        var databaseId = Convert.ToInt32(await this.Database.ExecuteScalarAsync("SELECT DB_ID();"), CultureInfo.InvariantCulture);
        var failures = new ConcurrentBag<Exception>();

        await this.Database.ExecuteScriptAsync(
            $"""
            CREATE EVENT SESSION [{session}] ON SERVER
            ADD EVENT sqlserver.error_reported (WHERE error_number = 2627 AND sqlserver.database_id = {databaseId})
            ADD TARGET package0.histogram (SET filtering_event_name = 'sqlserver.error_reported', source = 'error_number', source_type = 0)
            WITH (MAX_DISPATCH_LATENCY = 1 SECONDS);
            ALTER EVENT SESSION [{session}] ON SERVER STATE = START;
            """);

        try
        {
            await Task.WhenAll(Enumerable.Range(0, 50).Select(async _ =>
            {
                var identityMap = new SqlServerIdentityMap(this.ConnectionString);
                for (var i = 0; i < 10; i++)
                {
                    try
                    {
                        var identity = await identityMap.GetOrAddAsync(typeof(Bus), typeof(string), Guid.NewGuid().ToString("N"));
                        await identityMap.RemoveAsync(identity);
                    }
                    catch (Exception ex) when (ex is SqlException or PersistenceException)
                    {
                        failures.Add(ex);
                    }
                }
            }));

            await Assert.That(failures.Select(static failure => failure.Message).Distinct()).IsEmpty();
            await Assert.That(await this.CountCollisionsAsync(session)).IsEqualTo(0);
        }
        finally
        {
            await this.Database.ExecuteScriptAsync($"DROP EVENT SESSION [{session}] ON SERVER;");
        }
    }

    // Events reach the target asynchronously: read until the count holds still for two dispatch intervals.
    private async Task<int> CountCollisionsAsync(string session)
    {
        var previous = -1;
        for (var read = 0; read < 10; read++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5));

            var count = Convert.ToInt32(
                await this.Database.ExecuteScalarAsync(
                    $"""
                    SELECT COALESCE(SUM([Slot].value('@count', 'int')), 0)
                    FROM sys.dm_xe_sessions [Session]
                        INNER JOIN sys.dm_xe_session_targets [Target] ON [Session].[address] = [Target].[event_session_address]
                        CROSS APPLY (SELECT CAST([Target].[target_data] AS XML) AS [Data]) AS [Histogram]
                        CROSS APPLY [Histogram].[Data].nodes('/HistogramTarget/Slot') AS [Slots] ([Slot])
                    WHERE [Session].[name] = N'{session}';
                    """),
                CultureInfo.InvariantCulture);

            if (count == previous)
            {
                return count;
            }

            previous = count;
        }

        return previous;
    }

    private class Bus : AggregateRoot
    {
    }
}
