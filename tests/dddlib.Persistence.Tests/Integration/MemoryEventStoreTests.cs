using dddlib.Persistence.Memory;

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

    private sealed class Event
    {
        public int Id { get; set; }

        public string? Value { get; set; }
    }
}
