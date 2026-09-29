using dddlib.Persistence.EventDispatcher.Memory;
using dddlib.Persistence.Memory;
using Microsoft.Extensions.Time.Testing;

namespace dddlib.Persistence.EventDispatcher.Tests.Integration;

// Batching over the in-memory event store, with a fake clock for the batch timeout.
public class MemoryEventBatchStoreTests
{
    [Test]
    public async Task TryGetBatchFromEmptyEventStore()
    {
        var batchStore = new MemoryEventBatchStore(new MemoryEventStore());

        await Assert.That(await batchStore.GetNextBatchAsync(Guid.Empty, 50, TimeSpan.FromSeconds(30))).IsNull();
    }

    [Test]
    public async Task BatchesAreBoundedAndConsecutive()
    {
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore);
        var streamId = Guid.NewGuid();
        var state = await eventStore.CommitStreamAsync(streamId, [new Event { Id = 1 }, new Event { Id = 2 }], Guid.NewGuid(), null);
        await eventStore.CommitStreamAsync(streamId, [new Event { Id = 3 }], Guid.NewGuid(), state);

        var first = await batchStore.GetNextBatchAsync(Guid.Empty, 2, TimeSpan.FromSeconds(30));
        var second = await batchStore.GetNextBatchAsync(Guid.Empty, 2, TimeSpan.FromSeconds(30));
        var third = await batchStore.GetNextBatchAsync(Guid.Empty, 2, TimeSpan.FromSeconds(30));

        await Assert.That(first!.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1, 2]);
        await Assert.That(second!.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([3]);
        await Assert.That(third).IsNull();
        await Assert.That(first.Events[0].SequenceNumber).IsEqualTo(1);
        await Assert.That(second.Events[0].SequenceNumber).IsEqualTo(3);
    }

    [Test]
    public async Task DispatchersAreIndependent()
    {
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore);
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }], Guid.NewGuid(), null);

        var first = await batchStore.GetNextBatchAsync(Guid.NewGuid(), 50, TimeSpan.FromSeconds(30));
        var second = await batchStore.GetNextBatchAsync(Guid.NewGuid(), 50, TimeSpan.FromSeconds(30));

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNotNull();
    }

    [Test]
    public async Task MarkingDispatchedCompletesTheBatchAndAdvances()
    {
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore);
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }], Guid.NewGuid(), null);

        var batch = await batchStore.GetNextBatchAsync(Guid.Empty, 50, TimeSpan.FromSeconds(30));
        await batchStore.MarkDispatchedAsync(Guid.Empty, batch!.Events[^1].SequenceNumber);
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 2 }], Guid.NewGuid(), null);
        var next = await batchStore.GetNextBatchAsync(Guid.Empty, 50, TimeSpan.FromSeconds(30));

        await Assert.That(next!.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([2]);
    }

    [Test]
    public async Task AnAbandonedBatchIsHandedOutAgainAfterTheTimeout()
    {
        var clock = new FakeTimeProvider();
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore, clock);
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }], Guid.NewGuid(), null);

        var first = await batchStore.GetNextBatchAsync(Guid.Empty, 50, TimeSpan.FromSeconds(30));
        var whileInProgress = await batchStore.GetNextBatchAsync(Guid.Empty, 50, TimeSpan.FromSeconds(30));
        clock.Advance(TimeSpan.FromSeconds(31));
        var afterTimeout = await batchStore.GetNextBatchAsync(Guid.Empty, 50, TimeSpan.FromSeconds(30));

        await Assert.That(first).IsNotNull();
        await Assert.That(whileInProgress).IsNull();
        await Assert.That(afterTimeout!.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo(first!.Events.Select(static e => e.SequenceNumber));
    }

    [Test]
    public async Task DispatcherRetriesAFailedBatchInOrder()
    {
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore);
        var streamId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(streamId, [new Event { Id = 1 }, new Event { Id = 2 }, new Event { Id = 3 }], Guid.NewGuid(), null);

        var handled = new List<int>();
        var failOnce = true;
        var done = new TaskCompletionSource();
        await using var dispatcher = new Sdk.EventDispatcher(
            new CustomEventDispatcher((_, @event) =>
            {
                var id = ((Event)@event).Id;
                if (id == 2 && failOnce)
                {
                    failOnce = false;
                    throw new InvalidOperationException("transient");
                }

                handled.Add(id);
                if (id == 3)
                {
                    done.TrySetResult();
                }
            }),
            batchStore,
            new EventDispatcherOptions { BatchSize = 10, PollingInterval = TimeSpan.FromMilliseconds(20), BatchTimeout = TimeSpan.FromMilliseconds(200) });

        var failures = 0;
        dispatcher.DispatchFailed += (_, _) => failures++;
        dispatcher.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(failures).IsEqualTo(1);
        await Assert.That(handled).IsEquivalentTo([1, 2, 3]);
        await Assert.That(handled).IsInOrder();
    }

    private sealed class Event
    {
        public int Id { get; set; }
    }
}
