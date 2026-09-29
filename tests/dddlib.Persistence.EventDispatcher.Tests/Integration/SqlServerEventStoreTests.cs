using dddlib.Persistence.EventDispatcher.Sdk;
using dddlib.Persistence.EventDispatcher.SqlServer;
using dddlib.Persistence.SqlServer;

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
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString);

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

    private sealed class Event
    {
        public int Id { get; set; }

        public string? Value { get; set; }
    }
}
