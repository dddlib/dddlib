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

    // Two hosts share a dispatcher id: one takes a batch and stops, the other takes and completes the next. Marking the
    // later batch does not complete the earlier one, which comes back after the timeout
    // (https://github.com/dddlib/dddlib/issues/53).
    [Test]
    public async Task MarkingABatchDoesNotCompleteAnEarlierOneThatIsNotFinished()
    {
        var clock = new FakeTimeProvider();
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore, clock);
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }, new Event { Id = 3 }, new Event { Id = 4 }], Guid.NewGuid(), null);

        var abandoned = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        var later = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        await batchStore.MarkDispatchedAsync(dispatcherId, later!.Events[^1].SequenceNumber);
        var beforeTimeout = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        clock.Advance(TimeSpan.FromSeconds(31));
        var afterTimeout = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        var afterThat = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));

        await Assert.That(abandoned!.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1, 2]);
        await Assert.That(beforeTimeout).IsNull();
        await Assert.That(afterTimeout!.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1, 2]);
        await Assert.That(afterThat).IsNull();
    }

    // A host that stops partway through a batch marks what it dispatched. Restarted, it takes the next batch first, and
    // marking that does not complete the one it stopped in, whose rest comes back after the timeout.
    [Test]
    public async Task ARestartedHostGetsTheRestOfTheBatchItStoppedIn()
    {
        var clock = new FakeTimeProvider();
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore, clock);
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }, new Event { Id = 3 }, new Event { Id = 4 }], Guid.NewGuid(), null);

        var stoppedIn = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        await batchStore.MarkDispatchedAsync(dispatcherId, stoppedIn!.Events[0].SequenceNumber);
        var afterRestart = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        await batchStore.MarkDispatchedAsync(dispatcherId, afterRestart!.Events[^1].SequenceNumber);
        clock.Advance(TimeSpan.FromSeconds(31));
        var rest = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));

        await Assert.That(afterRestart.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([3, 4]);
        await Assert.That(rest!.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([2]);
    }

    // A batch that times out while a later one is still in progress comes back, though the later one ends further on.
    // Once both are marked the dispatcher carries on after them.
    [Test]
    public async Task ABatchThatTimesOutBehindOneInProgressComesBack()
    {
        var clock = new FakeTimeProvider();
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore, clock);
        var dispatcherId = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var state = await eventStore.CommitStreamAsync(streamId, [new Event { Id = 1 }, new Event { Id = 2 }, new Event { Id = 3 }, new Event { Id = 4 }], Guid.NewGuid(), null);

        var timedOut = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        clock.Advance(TimeSpan.FromSeconds(20));
        var inProgress = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        clock.Advance(TimeSpan.FromSeconds(15));
        var again = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        var nothingMore = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        await batchStore.MarkDispatchedAsync(dispatcherId, inProgress!.Events[^1].SequenceNumber);
        await batchStore.MarkDispatchedAsync(dispatcherId, again!.Events[^1].SequenceNumber);
        await eventStore.CommitStreamAsync(streamId, [new Event { Id = 5 }], Guid.NewGuid(), state);
        clock.Advance(TimeSpan.FromSeconds(31));
        var next = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));

        await Assert.That(timedOut!.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1, 2]);
        await Assert.That(inProgress.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([3, 4]);
        await Assert.That(again.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1, 2]);
        await Assert.That(nothingMore).IsNull();
        await Assert.That(next!.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([5]);
    }

    // The scenario of issue 53 end to end: another host with the same dispatcher id took a batch and stopped.
    [Test]
    public async Task EveryEventIsDispatchedWhenAnotherHostAbandonsABatch()
    {
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore);
        var options = new EventDispatcherOptions
        {
            DispatcherId = Guid.NewGuid(),
            BatchSize = 2,
            PollingInterval = TimeSpan.FromMilliseconds(20),
            MaxPollingInterval = TimeSpan.FromMilliseconds(20),
            BatchTimeout = TimeSpan.FromMilliseconds(300),
        };
        await eventStore.CommitStreamAsync(Guid.NewGuid(), Enumerable.Range(1, 6).Select(static id => (object)new Event { Id = id }).ToArray(), Guid.NewGuid(), null);
        await batchStore.GetNextBatchAsync(options.DispatcherId, options.BatchSize, options.BatchTimeout);

        var handled = new HashSet<int>();
        var done = new TaskCompletionSource();
        await using var dispatcher = new Sdk.EventDispatcher(
            new CustomEventDispatcher((_, @event) =>
            {
                lock (handled)
                {
                    handled.Add(((Event)@event).Id);
                    if (handled.Count == 6)
                    {
                        done.TrySetResult();
                    }
                }
            }),
            batchStore,
            options);

        dispatcher.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(handled).IsEquivalentTo([1, 2, 3, 4, 5, 6]);
    }

    // The in-memory event store keeps each event's metadata, as SQL Server does: here dddlib's hostname and timestamp
    // (https://github.com/dddlib/dddlib/issues/60).
    [Test]
    public async Task EventsCarryTheirMetadata()
    {
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore);
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }], Guid.NewGuid(), null);

        var batch = await batchStore.GetNextBatchAsync(Guid.Empty, 50, TimeSpan.FromSeconds(30));

        await Assert.That(batch!.Events[0].Metadata.Keys).IsEquivalentTo(["hostname", "timestamp"]);
        await Assert.That(batch.Events[0].Metadata["hostname"]).IsEqualTo(Environment.MachineName);
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
