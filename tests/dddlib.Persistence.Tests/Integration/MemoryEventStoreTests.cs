using dddlib.Persistence.Memory;
using dddlib.Persistence.Sdk;
using TUnit.Assertions.Enums;

namespace dddlib.Persistence.Tests.Integration;

public class MemoryEventStoreTests
{
    [Test]
    public async Task TrySaveSingleEvent()
    {
        var eventStore = new MemoryEventStore();
        var streamId = Guid.NewGuid();
        var events = new[] { new Event { Id = 1, Value = "One" } };

        var commitState = await eventStore.CommitStreamAsync(streamId, events, Guid.NewGuid(), null);
        var stream = await eventStore.GetStreamAsync(streamId, 0);

        await Assert.That(stream.Events).Count().IsEqualTo(1);
        await Assert.That(stream.Events[0]).IsTypeOf<Event>();
        await Assert.That(((Event)stream.Events[0]).Id).IsEqualTo(1);
        await Assert.That(((Event)stream.Events[0]).Value).IsEqualTo("One");
        await Assert.That(stream.Events[0]).IsNotSameReferenceAs(events[0]);
        await Assert.That(stream.State).IsEqualTo(commitState);
    }

    [Test]
    public async Task TrySaveMultipleEvents()
    {
        var eventStore = new MemoryEventStore();
        var streamId = Guid.NewGuid();
        var events = new[]
        {
            new Event { Id = 1, Value = "One" },
            new Event { Id = 2, Value = "Two" },
        };

        var commitState = await eventStore.CommitStreamAsync(streamId, events, Guid.NewGuid(), null);
        var stream = await eventStore.GetStreamAsync(streamId, 0);

        await Assert.That(stream.Events).Count().IsEqualTo(2);
        await Assert.That(((Event)stream.Events[0]).Id).IsEqualTo(1);
        await Assert.That(((Event)stream.Events[1]).Id).IsEqualTo(2);
        await Assert.That(stream.State).IsEqualTo(commitState);
    }

    [Test]
    public async Task TrySaveEventsInMultipleCommits()
    {
        var eventStore = new MemoryEventStore();
        var streamId = Guid.NewGuid();
        var events1 = new[] { new Event { Id = 1, Value = "One" } };
        var events2 = new[] { new Event { Id = 2, Value = "Two" } };

        var firstCommitState = await eventStore.CommitStreamAsync(streamId, events1, Guid.NewGuid(), null);
        var secondCommitState = await eventStore.CommitStreamAsync(streamId, events2, Guid.NewGuid(), firstCommitState);
        var stream = await eventStore.GetStreamAsync(streamId, 0);

        await Assert.That(stream.Events).Count().IsEqualTo(2);
        await Assert.That(((Event)stream.Events[0]).Id).IsEqualTo(1);
        await Assert.That(((Event)stream.Events[1]).Id).IsEqualTo(2);
        await Assert.That(stream.State).IsEqualTo(secondCommitState);
        await Assert.That(firstCommitState).IsNotEqualTo(secondCommitState);
    }

    [Test]
    public async Task TrySaveEventsForMultipleStreams()
    {
        var eventStore = new MemoryEventStore();
        var stream1Id = Guid.NewGuid();
        var stream2Id = Guid.NewGuid();
        var events1 = new[] { new Event { Id = 1, Value = "One" } };
        var events2 = new[] { new Event { Id = 2, Value = "Two" } };

        var stream1CommitState = await eventStore.CommitStreamAsync(stream1Id, events1, Guid.NewGuid(), null);
        var stream2CommitState = await eventStore.CommitStreamAsync(stream2Id, events2, Guid.NewGuid(), null);
        var stream1 = await eventStore.GetStreamAsync(stream1Id, 0);
        var stream2 = await eventStore.GetStreamAsync(stream2Id, 0);

        await Assert.That(stream1.Events).Count().IsEqualTo(1);
        await Assert.That(((Event)stream1.Events[0]).Id).IsEqualTo(1);
        await Assert.That(stream1.State).IsEqualTo(stream1CommitState);
        await Assert.That(stream2.Events).Count().IsEqualTo(1);
        await Assert.That(((Event)stream2.Events[0]).Id).IsEqualTo(2);
        await Assert.That(stream2.State).IsEqualTo(stream2CommitState);
    }

    [Test]
    public async Task ExpectClientSideConcurrencyException()
    {
        var eventStore = new MemoryEventStore();
        var streamId = Guid.NewGuid();
        var events1 = new[] { new Event { Id = 1, Value = "One" } };
        var events2 = new[] { new Event { Id = 1, Value = "AnotherOne" } };

        await eventStore.CommitStreamAsync(streamId, events1, Guid.NewGuid(), null);
        Func<Task> action = () => eventStore.CommitStreamAsync(streamId, events2, Guid.NewGuid(), null);

        await Assert.That(action).Throws<ConcurrencyException>();
    }

    // The legacy test was skipped because the memory store's mutex was re-entrant on the test thread. The v2 store
    // has no cross-process mutex, so the intent is expressed as a race: of many concurrent initial commits to the
    // same stream exactly one wins.
    [Test]
    public async Task ExpectServerSideConcurrencyException()
    {
        var eventStore = new MemoryEventStore();
        var streamId = Guid.NewGuid();

        var outcomes = await Task.WhenAll(
            Enumerable.Range(1, 16).Select(id => Task.Run(async () =>
            {
                try
                {
                    await eventStore.CommitStreamAsync(streamId, [new Event { Id = id, Value = "One" }], Guid.NewGuid(), null);
                    return true;
                }
                catch (ConcurrencyException)
                {
                    return false;
                }
            })));

        var stream = await eventStore.GetStreamAsync(streamId, 0);

        await Assert.That(outcomes.Count(succeeded => succeeded)).IsEqualTo(1);
        await Assert.That(stream.Events).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ReadsEventsInSequenceOrder()
    {
        var eventStore = new MemoryEventStore();
        var stream1Id = Guid.NewGuid();
        var stream2Id = Guid.NewGuid();
        var correlation1 = Guid.NewGuid();
        var correlation2 = Guid.NewGuid();
        var events = new[] { new Event { Id = 1, Value = "One" }, new Event { Id = 2, Value = "Two" } };
        await eventStore.CommitStreamAsync(stream1Id, events, correlation1, null);
        await eventStore.CommitStreamAsync(stream2Id, [new Event { Id = 3, Value = "Three" }], correlation2, null);

        var page = await eventStore.ReadEventsAsync(0, 10);

        await Assert.That(page.EndSequenceNumber).IsEqualTo(3);
        await Assert.That(page.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([1L, 2L, 3L]);
        await Assert.That(page.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1, 2, 3]);
        await Assert.That(page.Events.Select(static e => e.StreamId)).IsEquivalentTo([stream1Id, stream1Id, stream2Id]);
        await Assert.That(page.Events.Select(static e => e.StreamRevision)).IsEquivalentTo([1, 2, 1]);
        await Assert.That(page.Events.Select(static e => e.CorrelationId)).IsEquivalentTo([correlation1, correlation1, correlation2]);
        await Assert.That(page.Events[0].Event).IsNotSameReferenceAs(events[0]);
    }

    [Test]
    public async Task ReadsOnlyAfterTheSequenceNumber()
    {
        var eventStore = new MemoryEventStore();
        var streamId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(streamId, [new Event { Id = 1 }, new Event { Id = 2 }, new Event { Id = 3 }], Guid.NewGuid(), null);

        var second = await eventStore.ReadEventsAsync(1, 1);
        var rest = await eventStore.ReadEventsAsync(2, 10);
        var none = await eventStore.ReadEventsAsync(3, 10);

        await Assert.That(second.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([2L]);
        await Assert.That(second.EndSequenceNumber).IsEqualTo(2);
        await Assert.That(rest.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([3L]);
        await Assert.That(rest.EndSequenceNumber).IsEqualTo(3);
        await Assert.That(none.Events).IsEmpty();
        await Assert.That(none.EndSequenceNumber).IsEqualTo(3);
    }

    // The page spans every event so the reader's checkpoint moves past the ones it did not ask for.
    [Test]
    public async Task ReadsOnlyTheRequestedEventTypes()
    {
        var eventStore = new MemoryEventStore();
        var streamId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(streamId, [new Event { Id = 1 }, new OtherEvent(), new Event { Id = 3 }], Guid.NewGuid(), null);

        var others = await eventStore.ReadEventsAsync(0, 10, [typeof(OtherEvent)]);
        var firstPage = await eventStore.ReadEventsAsync(0, 2, [typeof(Event)]);
        var all = await eventStore.ReadEventsAsync(0, 10, []);

        await Assert.That(others.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([2L]);
        await Assert.That(others.EndSequenceNumber).IsEqualTo(3);
        await Assert.That(firstPage.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([1L]);
        await Assert.That(firstPage.EndSequenceNumber).IsEqualTo(2);
        await Assert.That(all.Events).Count().IsEqualTo(3);
    }

    [Test]
    public async Task ReportsTheLastSequenceNumber()
    {
        var eventStore = new MemoryEventStore();

        var empty = await eventStore.GetLastSequenceNumberAsync();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }], Guid.NewGuid(), null);
        var two = await eventStore.GetLastSequenceNumberAsync();

        await Assert.That(empty).IsEqualTo(0);
        await Assert.That(two).IsEqualTo(2);
    }

    [Test]
    public async Task CommitStreamsAssignsContiguousSequenceNumbers()
    {
        var eventStore = new MemoryEventStore();
        var stream1Id = Guid.NewGuid();
        var stream2Id = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 0 }], Guid.NewGuid(), null);
        var first = await eventStore.GetLastSequenceNumberAsync();

        var states = await eventStore.CommitStreamsAsync(
            [
                new StreamCommit(stream1Id, [new Event { Id = 1 }, new Event { Id = 2 }], null),
                new StreamCommit(stream2Id, [new Event { Id = 3 }], null),
            ],
            correlationId);
        var page = await eventStore.ReadEventsAsync(first, 10);
        var stream1 = await eventStore.GetStreamAsync(stream1Id, 0);
        var stream2 = await eventStore.GetStreamAsync(stream2Id, 0);

        await Assert.That(states).Count().IsEqualTo(2);
        await Assert.That(stream1.State).IsEqualTo(states[0]);
        await Assert.That(stream2.State).IsEqualTo(states[1]);
        await Assert.That(page.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([first + 1, first + 2, first + 3], CollectionOrdering.Matching);
        await Assert.That(page.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1, 2, 3], CollectionOrdering.Matching);
        await Assert.That(page.Events.Select(static e => e.StreamRevision)).IsEquivalentTo([1, 2, 1], CollectionOrdering.Matching);
        await Assert.That(page.Events.Select(static e => e.CorrelationId).Distinct()).IsEquivalentTo([correlationId]);
    }

    [Test]
    public async Task CommitStreamsNamesTheStaleStream()
    {
        var eventStore = new MemoryEventStore();
        var currentId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        var current = await eventStore.CommitStreamAsync(currentId, [new Event { Id = 1 }], Guid.NewGuid(), null);
        var stale = await eventStore.CommitStreamAsync(staleId, [new Event { Id = 2 }], Guid.NewGuid(), null);
        await eventStore.CommitStreamAsync(staleId, [new Event { Id = 3 }], Guid.NewGuid(), stale);
        var last = await eventStore.GetLastSequenceNumberAsync();

        Func<Task> commit = () => eventStore.CommitStreamsAsync(
            [
                new StreamCommit(currentId, [new Event { Id = 4 }], current),
                new StreamCommit(staleId, [new Event { Id = 5 }], stale),
            ],
            Guid.NewGuid());
        Func<Task> create = () => eventStore.CommitStreamsAsync([new StreamCommit(currentId, [new Event { Id = 6 }], null)], Guid.NewGuid());

        var exception = await Assert.That(commit).Throws<StreamConcurrencyException>();
        var exists = await Assert.That(create).Throws<StreamConcurrencyException>();

        await Assert.That(exception!.StreamId).IsEqualTo(staleId);
        await Assert.That(exists!.StreamId).IsEqualTo(currentId);
        await Assert.That(await eventStore.GetLastSequenceNumberAsync()).IsEqualTo(last);
        await Assert.That((await eventStore.GetStreamAsync(currentId, 0)).State).IsEqualTo(current);
    }

    [Test]
    public async Task CommitStreamsRejectsTheSameStreamTwice()
    {
        var eventStore = new MemoryEventStore();
        var streamId = Guid.NewGuid();
        var last = await eventStore.GetLastSequenceNumberAsync();

        Func<Task> commit = () => eventStore.CommitStreamsAsync(
            [
                new StreamCommit(streamId, [new Event { Id = 1 }], null),
                new StreamCommit(streamId, [new Event { Id = 2 }], null),
            ],
            Guid.NewGuid());

        await Assert.That(commit).Throws<ArgumentException>();
        await Assert.That(await eventStore.GetLastSequenceNumberAsync()).IsEqualTo(last);
    }

    private sealed class Event
    {
        public int Id { get; set; }

        public string? Value { get; set; }
    }

    private sealed class OtherEvent
    {
        public string? Value { get; set; }
    }
}
