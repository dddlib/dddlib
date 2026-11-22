using System.Data;
using dddlib.Persistence.EventDispatcher.Sdk;
using dddlib.Persistence.EventDispatcher.SqlServer;
using dddlib.Persistence.SqlServer;
using Microsoft.Data.SqlClient;
using TUnit.Assertions.Enums;

namespace dddlib.Persistence.EventDispatcher.Tests.Integration;

// Batching over the SQL Server event store. The tests share one database and the batch feed is store-wide, so they
// run one at a time: a test that expects "nothing more to dispatch" must not race another test's commit.
[NotInParallel]
public class SqlServerEventStoreTests
{
    [ClassDataSource<DispatcherDatabase>(Shared = SharedType.PerClass)]
    public required DispatcherDatabase Database { get; init; }

    private string ConnectionString => this.Database.ConnectionString;

    [Test]
    public async Task TryGetBatchFromEmptyEventStore()
    {
        // A schema of its own: the other tests commit to the shared one, and the order the tests run in is not fixed.
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));
        await this.Database.Database.CreateSchemaAsync(schema);
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString, schema);

        var batch = await batchStore.GetNextBatchAsync(Guid.NewGuid(), 50, TimeSpan.FromSeconds(30));

        await Assert.That(batch).IsNull();
    }

    [Test]
    public async Task TryGetBatchFromEventStoreWithSingleEvent()
    {
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString);
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1, Value = "One" }], Guid.NewGuid(), null);

        var batch = await batchStore.GetNextBatchAsync(Guid.NewGuid(), 50, TimeSpan.FromSeconds(30));

        await Assert.That(batch).IsNotNull();
        await Assert.That(batch!.Events.Select(static e => e.Event).OfType<Event>().Where(static e => e.Id == 1 && e.Value == "One")).HasSingleItem();
    }

    [Test]
    public async Task TryGetBatchTwiceFromEventStoreWithSingleEvent()
    {
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString);
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 2, Value = "Two" }], Guid.NewGuid(), null);

        var firstBatch = await batchStore.GetNextBatchAsync(dispatcherId, 50, TimeSpan.FromSeconds(30));
        var secondBatch = await batchStore.GetNextBatchAsync(dispatcherId, 50, TimeSpan.FromSeconds(30));

        await Assert.That(firstBatch).IsNotNull();
        await Assert.That(firstBatch!.Events.Select(static e => e.Event).OfType<Event>().Where(static e => e.Id == 2)).HasSingleItem();
        await Assert.That(secondBatch).IsNull();
    }

    [Test]
    public async Task TryGetBatchTwiceFromEventStoreWithSingleEventAndDifferentDispatchers()
    {
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString);
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 3, Value = "Three" }], Guid.NewGuid(), null);

        var firstBatch = await batchStore.GetNextBatchAsync(Guid.NewGuid(), 50, TimeSpan.FromSeconds(30));
        var secondBatch = await batchStore.GetNextBatchAsync(Guid.NewGuid(), 50, TimeSpan.FromSeconds(30));

        await Assert.That(firstBatch!.Events.Select(static e => e.Event).OfType<Event>().Where(static e => e.Id == 3)).HasSingleItem();
        await Assert.That(secondBatch!.Events.Select(static e => e.Event).OfType<Event>().Where(static e => e.Id == 3)).HasSingleItem();
    }

    [Test]
    public async Task TryGetMultipleBatchesFromEventStoreWithManyEvents()
    {
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString);
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var dispatcherId = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var state = await eventStore.CommitStreamAsync(streamId, [new Event { Id = 4, Value = "Four" }, new Event { Id = 5, Value = "Five" }], Guid.NewGuid(), null);
        await eventStore.CommitStreamAsync(streamId, [new Event { Id = 6, Value = "Six" }], Guid.NewGuid(), state);

        // The other tests may have committed events too, so the expectation is about this stream's events only.
        var batches = new List<EventBatch>();
        while (await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30)) is { } batch)
        {
            batches.Add(batch);
            await Assert.That(batch.Events.Count).IsLessThanOrEqualTo(2);
            await batchStore.MarkDispatchedAsync(dispatcherId, batch.Events[^1].SequenceNumber);
        }

        var ids = batches.SelectMany(static b => b.Events).Select(static e => e.Event).OfType<Event>().Select(static e => e.Id).Where(static id => id is 4 or 5 or 6).ToList();

        await Assert.That(ids).IsEquivalentTo([4, 5, 6]);
        await Assert.That(ids).IsInOrder();
    }

    [Test]
    public async Task MarkingDispatchedCompletesTheBatchAndAdvances()
    {
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString);
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 7, Value = "Seven" }], Guid.NewGuid(), null);

        var batch = await batchStore.GetNextBatchAsync(dispatcherId, 50, TimeSpan.FromSeconds(30));
        await batchStore.MarkDispatchedAsync(dispatcherId, batch!.Events[^1].SequenceNumber);
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 8, Value = "Eight" }], Guid.NewGuid(), null);
        var nextBatch = await batchStore.GetNextBatchAsync(dispatcherId, 50, TimeSpan.FromSeconds(30));

        await Assert.That(nextBatch).IsNotNull();
        await Assert.That(nextBatch!.Events.Select(static e => e.Event).OfType<Event>().Select(static e => e.Id)).IsEquivalentTo([8]);
    }

    [Test]
    public async Task AnAbandonedBatchIsHandedOutAgainAfterTheTimeout()
    {
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString);
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 9, Value = "Nine" }], Guid.NewGuid(), null);

        var firstBatch = await batchStore.GetNextBatchAsync(dispatcherId, 50, TimeSpan.FromSeconds(1));
        var whileInProgress = await batchStore.GetNextBatchAsync(dispatcherId, 50, TimeSpan.FromSeconds(1));
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var afterTimeout = await batchStore.GetNextBatchAsync(dispatcherId, 50, TimeSpan.FromSeconds(1));

        await Assert.That(firstBatch).IsNotNull();
        await Assert.That(whileInProgress).IsNull();
        await Assert.That(afterTimeout).IsNotNull();
        await Assert.That(afterTimeout!.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo(firstBatch!.Events.Select(static e => e.SequenceNumber));
    }

    // Two hosts share a dispatcher id. While one has a batch in flight the other gets nothing, so events are dispatched
    // in order. A batch that is not finished comes back after the timeout, before any later batch
    // (https://github.com/dddlib/dddlib/issues/53).
    [Test]
    public async Task AHostWaitsWhileAnotherHasABatchInFlight()
    {
        var (eventStore, batchStore) = await this.CreateStoresAsync();
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }, new Event { Id = 3 }, new Event { Id = 4 }], Guid.NewGuid(), null);

        var inFlight = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(1));
        var whileInFlight = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(1));
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var again = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(1));
        await batchStore.MarkDispatchedAsync(dispatcherId, again!.Events[^1].SequenceNumber);
        var next = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(1));

        await Assert.That(Ids(inFlight)).IsEquivalentTo([1, 2]);
        await Assert.That(whileInFlight).IsNull();
        await Assert.That(Ids(again)).IsEquivalentTo([1, 2]);
        await Assert.That(Ids(next)).IsEquivalentTo([3, 4]);
    }

    // A host that stops partway through a batch marks what it dispatched. Restarted, it gets nothing until the batch
    // times out, and then the rest of it, before the next batch.
    [Test]
    public async Task ARestartedHostGetsTheRestOfTheBatchItStoppedIn()
    {
        var (eventStore, batchStore) = await this.CreateStoresAsync();
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }, new Event { Id = 3 }, new Event { Id = 4 }], Guid.NewGuid(), null);

        var stoppedIn = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(1));
        await batchStore.MarkDispatchedAsync(dispatcherId, stoppedIn!.Events[0].SequenceNumber);
        var afterRestart = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(1));
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var rest = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(1));
        await batchStore.MarkDispatchedAsync(dispatcherId, rest!.Events[^1].SequenceNumber);
        var next = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(1));

        await Assert.That(afterRestart).IsNull();
        await Assert.That(Ids(rest)).IsEquivalentTo([2]);
        await Assert.That(Ids(next)).IsEquivalentTo([3, 4]);
    }

    // A host that stops releases its batch, so the rest of it comes back at once rather than after the timeout.
    [Test]
    public async Task AReleasedBatchComesBackAtOnce()
    {
        var (eventStore, batchStore) = await this.CreateStoresAsync();
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }, new Event { Id = 3 }], Guid.NewGuid(), null);

        var released = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        await batchStore.MarkDispatchedAsync(dispatcherId, released!.Events[0].SequenceNumber);
        await batchStore.ReleaseAsync(dispatcherId, released.Id);
        var rest = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));

        await Assert.That(Ids(rest)).IsEquivalentTo([2]);
    }

    // A host that held a batch past the timeout no longer holds it: releasing it then does not release the batch that
    // another host has taken since.
    [Test]
    public async Task ReleasingABatchHandedOutAgainSinceDoesNothing()
    {
        var (eventStore, batchStore) = await this.CreateStoresAsync();
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }], Guid.NewGuid(), null);

        var timedOut = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(1));
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var again = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(1));
        await batchStore.ReleaseAsync(dispatcherId, timedOut!.Id);
        var whileInFlight = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));

        await Assert.That(again!.Id).IsNotEqualTo(timedOut.Id);
        await Assert.That(whileInFlight).IsNull();
    }

    // The scenario of issue 53 end to end: another host with the same dispatcher id took a batch and stopped without
    // releasing it. Every event is dispatched, in order, once the batch has timed out.
    [Test]
    public async Task EveryEventIsDispatchedInOrderWhenAnotherHostAbandonsABatch()
    {
        var (eventStore, batchStore) = await this.CreateStoresAsync();
        var options = new EventDispatcherOptions
        {
            DispatcherId = Guid.NewGuid(),
            BatchSize = 2,
            PollingInterval = TimeSpan.FromMilliseconds(20),
            MaxPollingInterval = TimeSpan.FromMilliseconds(20),
            BatchTimeout = TimeSpan.FromMilliseconds(500),
        };
        await eventStore.CommitStreamAsync(Guid.NewGuid(), Enumerable.Range(1, 6).Select(static id => (object)new Event { Id = id }).ToArray(), Guid.NewGuid(), null);
        await batchStore.GetNextBatchAsync(options.DispatcherId, options.BatchSize, options.BatchTimeout);

        var handled = new List<int>();
        var done = new TaskCompletionSource();
        await using var dispatcher = new Sdk.EventDispatcher(
            new CustomEventDispatcher((_, @event) =>
            {
                handled.Add(((Event)@event).Id);
                if (handled.Count == 6)
                {
                    done.TrySetResult();
                }
            }),
            batchStore,
            options);

        dispatcher.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(handled).IsEquivalentTo([1, 2, 3, 4, 5, 6], CollectionOrdering.Matching);
    }

    // A host that is stopped partway through a batch marks what it dispatched and releases the rest, so the next host
    // carries on at once, in order, long before the batch would time out.
    [Test]
    public async Task StoppingAHostReleasesItsBatch()
    {
        var (eventStore, batchStore) = await this.CreateStoresAsync();
        var options = new EventDispatcherOptions
        {
            DispatcherId = Guid.NewGuid(),
            BatchSize = 10,
            PollingInterval = TimeSpan.FromMilliseconds(20),
            MaxPollingInterval = TimeSpan.FromMilliseconds(20),
            BatchTimeout = TimeSpan.FromMinutes(5),
        };
        await eventStore.CommitStreamAsync(Guid.NewGuid(), Enumerable.Range(1, 4).Select(static id => (object)new Event { Id = id }).ToArray(), Guid.NewGuid(), null);

        var first = new List<int>();
        var stuck = new TaskCompletionSource();
        await using (var stopping = new Sdk.EventDispatcher(
            new CustomEventDispatcher(async (_, @event, cancellationToken) =>
            {
                if (((Event)@event).Id == 2)
                {
                    stuck.TrySetResult();
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                first.Add(((Event)@event).Id);
            }),
            batchStore,
            options))
        {
            stopping.Start();
            await stuck.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await stopping.StopAsync();
        }

        var second = new List<int>();
        var done = new TaskCompletionSource();
        await using var next = new Sdk.EventDispatcher(
            new CustomEventDispatcher((_, @event) =>
            {
                second.Add(((Event)@event).Id);
                if (((Event)@event).Id == 4)
                {
                    done.TrySetResult();
                }
            }),
            batchStore,
            options);
        next.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(first).IsEquivalentTo([1]);
        await Assert.That(second).IsEquivalentTo([2, 3, 4], CollectionOrdering.Matching);
    }

    [Test]
    public async Task EventsCarryTheMetadataTheyWereStoredWith()
    {
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString);
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 10, Value = "Ten" }], Guid.NewGuid(), null);

        var metadata = (await ReadAllAsync(batchStore)).Single(static e => e.Event is Event { Id: 10 }).Metadata;

        await Assert.That(metadata.Keys).IsEquivalentTo(["hostname", "timestamp"]);
        await Assert.That(metadata["hostname"]).IsEqualTo(Environment.MachineName);
    }

    // Metadata the batch store cannot read as string entries is absent rather than failing the batch, which would
    // otherwise be handed out, and fail, again and again.
    [Test]
    [Arguments(11, null, new string[0])]
    [Arguments(12, """{"hostname":"web-01","count":3,"nested":{"tenant":"acme"},"tenant":null}""", new[] { "hostname=web-01" })]
    [Arguments(13, "[]", new string[0])]
    [Arguments(14, "not json", new string[0])]
    public async Task MetadataThatIsMissingOrNotAStringIsAbsent(int id, string? storedMetadata, string[] expected)
    {
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString);
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = id }], Guid.NewGuid(), null);
        await using (var connection = new SqlConnection(this.ConnectionString))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE [dbo].[Events] SET [Metadata] = @Metadata WHERE [SequenceNumber] = @SequenceNumber;";
            command.Parameters.Add("@Metadata", SqlDbType.NVarChar, -1).Value = (object?)storedMetadata ?? DBNull.Value;
            command.Parameters.Add("@SequenceNumber", SqlDbType.BigInt).Value = await eventStore.GetLastSequenceNumberAsync();
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        var metadata = (await ReadAllAsync(batchStore)).Single(e => e.Event is Event @event && @event.Id == id).Metadata;

        await Assert.That(metadata.Select(static entry => $"{entry.Key}={entry.Value}")).IsEquivalentTo(expected);
    }

    private static IEnumerable<int> Ids(EventBatch? batch) => batch!.Events.Select(static e => ((Event)e.Event).Id);

    // Stores over a schema of their own, so the events are only the test's.
    private async Task<(SqlServerEventStore EventStore, SqlServerEventBatchStore BatchStore)> CreateStoresAsync()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));
        await this.Database.Database.CreateSchemaAsync(schema);
        return (new SqlServerEventStore(this.ConnectionString, schema), new SqlServerEventBatchStore(this.ConnectionString, schema));
    }

    // Every event in the store, for a dispatcher of its own.
    private static async Task<List<DispatchedEvent>> ReadAllAsync(SqlServerEventBatchStore batchStore)
    {
        var dispatcherId = Guid.NewGuid();
        var events = new List<DispatchedEvent>();
        while (await batchStore.GetNextBatchAsync(dispatcherId, 50, TimeSpan.FromSeconds(30)) is { } batch)
        {
            events.AddRange(batch.Events);
            await batchStore.MarkDispatchedAsync(dispatcherId, batch.Events[^1].SequenceNumber);
        }

        return events;
    }

    private sealed class Event
    {
        public int Id { get; set; }

        public string? Value { get; set; }
    }
}
