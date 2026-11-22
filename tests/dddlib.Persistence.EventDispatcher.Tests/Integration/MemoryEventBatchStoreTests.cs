using dddlib.Persistence.EventDispatcher.Memory;
using dddlib.Persistence.EventDispatcher.Sdk;
using dddlib.Persistence.Memory;
using Microsoft.Extensions.Time.Testing;
using TUnit.Assertions.Enums;

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
        await batchStore.MarkDispatchedAsync(Guid.Empty, first!.Events[^1].SequenceNumber);
        var second = await batchStore.GetNextBatchAsync(Guid.Empty, 2, TimeSpan.FromSeconds(30));
        await batchStore.MarkDispatchedAsync(Guid.Empty, second!.Events[^1].SequenceNumber);
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

    // Two hosts share a dispatcher id. While one has a batch in flight the other gets nothing, so events are dispatched
    // in order. A batch that is not finished comes back after the timeout, before any later batch
    // (https://github.com/dddlib/dddlib/issues/53).
    [Test]
    public async Task AHostWaitsWhileAnotherHasABatchInFlight()
    {
        var clock = new FakeTimeProvider();
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore, clock);
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }, new Event { Id = 3 }, new Event { Id = 4 }], Guid.NewGuid(), null);

        var inFlight = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        var whileInFlight = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        clock.Advance(TimeSpan.FromSeconds(31));
        var again = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        await batchStore.MarkDispatchedAsync(dispatcherId, again!.Events[^1].SequenceNumber);
        var next = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));

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
        var clock = new FakeTimeProvider();
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore, clock);
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }, new Event { Id = 3 }, new Event { Id = 4 }], Guid.NewGuid(), null);

        var stoppedIn = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        await batchStore.MarkDispatchedAsync(dispatcherId, stoppedIn!.Events[0].SequenceNumber);
        var afterRestart = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        clock.Advance(TimeSpan.FromSeconds(31));
        var rest = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        await batchStore.MarkDispatchedAsync(dispatcherId, rest!.Events[^1].SequenceNumber);
        var next = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));

        await Assert.That(afterRestart).IsNull();
        await Assert.That(Ids(rest)).IsEquivalentTo([2]);
        await Assert.That(Ids(next)).IsEquivalentTo([3, 4]);
    }

    // A host that stops releases its batch, so the rest of it comes back at once rather than after the timeout.
    [Test]
    public async Task AReleasedBatchComesBackAtOnce()
    {
        var clock = new FakeTimeProvider();
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore, clock);
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
        var clock = new FakeTimeProvider();
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore, clock);
        var dispatcherId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }], Guid.NewGuid(), null);

        var timedOut = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
        clock.Advance(TimeSpan.FromSeconds(31));
        var again = await batchStore.GetNextBatchAsync(dispatcherId, 2, TimeSpan.FromSeconds(30));
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
        var eventStore = new MemoryEventStore();
        var batchStore = new MemoryEventBatchStore(eventStore);
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

    private static IEnumerable<int> Ids(EventBatch? batch) => batch!.Events.Select(static e => ((Event)e.Event).Id);

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
