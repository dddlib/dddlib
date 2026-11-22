using System.Collections.Concurrent;
using System.Globalization;
using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.Tests.Integration;

// Every add and removal of a natural key writes at the next checkpoint of its type. Writers that did not take turns
// read the same latest checkpoint, collided on the primary key and retried: under 50 writers thousands of collisions in
// 1000 operations, with the unluckiest removals waiting 10 to 20 seconds and timing out under load. How long the
// writers wait in turn depends on the machine; how often they collide does not, so that is what this class measures.
// It has a database of its own, and runs one test at a time, so that the collisions it counts are its own.
[NotInParallel(nameof(SqlServerNaturalKeyContentionTests))]
public class SqlServerNaturalKeyContentionTests : SqlServerIntegration
{
    [Test]
    public async Task ConcurrentWritersOfATypeTakeTurnsAtItsCheckpoints()
    {
        var failures = new ConcurrentBag<Exception>();

        var collisions = await this.CountCollisionsDuringAsync(() => Task.WhenAll(Enumerable.Range(0, 50).Select(async _ =>
        {
            var identityMap = new SqlServerIdentityMap(this.ConnectionString);
            await AddAndRemoveAsync(identityMap, typeof(Bus), failures);
        })));

        await Assert.That(failures.Select(static failure => failure.Message).Distinct()).IsEmpty();
        await Assert.That(collisions).IsEqualTo(0);
    }

    // A key the identity map compares itself is added at the checkpoint the writer last synchronized to, and fails back
    // to it when another writer has since added a key. That add now takes the type's turn too, so it no longer collides
    // on the checkpoint, and its writers synchronize while others remove keys, which must not deadlock either
    // (https://github.com/dddlib/dddlib/issues/57). Ten writers, not fifty: the protocol is optimistic, and with fifty
    // writers adding keys of one type the unluckiest are still sent back to synchronize until they run out of attempts.
    [Test]
    public async Task WritersOfKeysTheIdentityMapComparesTakeTurnsAtItsCheckpoints()
    {
        var failures = new ConcurrentBag<Exception>();

        var collisions = await this.CountCollisionsDuringAsync(() => Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            var identityMap = new DefaultIdentityMap(new SqlServerNaturalKeyRepository(this.ConnectionString), new NonCanonicalSerializer());
            await AddAndRemoveAsync(identityMap, typeof(Coach), failures);
        })));

        await Assert.That(failures.Select(static failure => failure.Message).Distinct()).IsEmpty();
        await Assert.That(collisions).IsEqualTo(0);
    }

    private static async Task AddAndRemoveAsync(IIdentityMap identityMap, Type aggregateRootType, ConcurrentBag<Exception> failures)
    {
        for (var i = 0; i < 10; i++)
        {
            try
            {
                var identity = await identityMap.GetOrAddAsync(aggregateRootType, typeof(string), Guid.NewGuid().ToString("N"));
                await identityMap.RemoveAsync(identity);
            }
            catch (Exception ex) when (ex is SqlException or PersistenceException)
            {
                failures.Add(ex);
            }
        }
    }

    private async Task<int> CountCollisionsDuringAsync(Func<Task> writes)
    {
        var session = string.Concat("dddlib_", Guid.NewGuid().ToString("N"));
        var databaseId = Convert.ToInt32(await this.Database.ExecuteScalarAsync("SELECT DB_ID();"), CultureInfo.InvariantCulture);

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
            await writes();
            return await this.CountCollisionsAsync(session);
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

    private class Coach : AggregateRoot
    {
    }

    // The default serialization, with keys compared by the identity map, as for a key with a case-insensitive comparer.
    private sealed class NonCanonicalSerializer : INaturalKeySerializer
    {
        private readonly DefaultNaturalKeySerializer serializer = new();

        public string Serialize(Type naturalKeyType, object naturalKey) => this.serializer.Serialize(naturalKeyType, naturalKey);

        public object Deserialize(Type naturalKeyType, string serializedNaturalKey) => this.serializer.Deserialize(naturalKeyType, serializedNaturalKey);
    }
}
