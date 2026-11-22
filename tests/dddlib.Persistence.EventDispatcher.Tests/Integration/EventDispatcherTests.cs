using dddlib.Persistence.EventDispatcher.Memory;
using dddlib.Persistence.EventDispatcher.Sdk;
using dddlib.Persistence.Memory;

namespace dddlib.Persistence.EventDispatcher.Tests.Integration;

// How the host marks events dispatched: once per batch rather than once per event, and only up to the last event it
// knows was dispatched (https://github.com/dddlib/dddlib/issues/59).
public class EventDispatcherTests
{
    private static readonly EventDispatcherOptions Options = new()
    {
        BatchSize = 10,
        PollingInterval = TimeSpan.FromMilliseconds(20),
        BatchTimeout = TimeSpan.FromMilliseconds(200),
    };

    [Test]
    public async Task MarksDispatchedOncePerBatch()
    {
        var batchStore = await CreateBatchStoreAsync(3);
        var handled = new List<int>();

        await RunUntilMarkedAsync(new CustomEventDispatcher((_, @event) => handled.Add(((Event)@event).Id)), batchStore, 3);

        await Assert.That(handled).IsEquivalentTo([1, 2, 3]);
        await Assert.That(batchStore.Marks).IsEquivalentTo([3L]);
    }

    [Test]
    public async Task AFailedBatchMarksTheEventsBeforeTheFailure()
    {
        var batchStore = await CreateBatchStoreAsync(4);
        var failOnce = true;

        var failures = await RunUntilMarkedAsync(
            new CustomEventDispatcher((_, @event) =>
            {
                if (((Event)@event).Id == 3 && failOnce)
                {
                    failOnce = false;
                    throw new InvalidOperationException("transient");
                }
            }),
            batchStore,
            4);

        await Assert.That(failures.Select(static f => f.SequenceNumber)).IsEquivalentTo([3L]);
        await Assert.That(batchStore.Marks).IsEquivalentTo([2L, 4L]);
    }

    [Test]
    public async Task AFailureOnTheFirstEventMarksNothing()
    {
        var batchStore = await CreateBatchStoreAsync(2);
        var failOnce = true;

        var failures = await RunUntilMarkedAsync(
            new CustomEventDispatcher((_, _) =>
            {
                if (failOnce)
                {
                    failOnce = false;
                    throw new InvalidOperationException("transient");
                }
            }),
            batchStore,
            2);

        await Assert.That(failures.Select(static f => f.SequenceNumber)).IsEquivalentTo([1L]);
        await Assert.That(batchStore.Marks).IsEquivalentTo([2L]);
    }

    [Test]
    public async Task StoppingMidBatchMarksTheEventsDispatched()
    {
        var batchStore = await CreateBatchStoreAsync(3);
        var handled = new List<int>();
        using var stopping = new CancellationTokenSource();
        var dispatcher = new Sdk.EventDispatcher(
            new CustomEventDispatcher((_, @event) =>
            {
                handled.Add(((Event)@event).Id);
                stopping.Cancel();
            }),
            batchStore,
            Options);

        await dispatcher.RunAsync(stopping.Token).WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(handled).IsEquivalentTo([1]);
        await Assert.That(batchStore.Marks).IsEquivalentTo([1L]);
    }

    [Test]
    public async Task ABatchDispatcherReceivesTheWholeBatch()
    {
        var batchStore = await CreateBatchStoreAsync(3);
        var batches = new List<long[]>();

        await RunUntilMarkedAsync(new BatchDispatcher(events => batches.Add(events.Select(static e => e.SequenceNumber).ToArray())), batchStore, 3);

        await Assert.That(batches).HasSingleItem();
        await Assert.That(batches[0]).IsEquivalentTo([1L, 2L, 3L]);
        await Assert.That(batchStore.Marks).IsEquivalentTo([3L]);
    }

    [Test]
    public async Task AFailedBatchDispatchIsRetriedWhole()
    {
        var batchStore = await CreateBatchStoreAsync(3);
        var batches = new List<long[]>();
        var failOnce = true;

        var failures = await RunUntilMarkedAsync(
            new BatchDispatcher(events =>
            {
                batches.Add(events.Select(static e => e.SequenceNumber).ToArray());
                if (failOnce)
                {
                    failOnce = false;
                    throw new InvalidOperationException("transient");
                }
            }),
            batchStore,
            3);

        await Assert.That(failures.Select(static f => f.SequenceNumber)).IsEquivalentTo([1L]);
        await Assert.That(failures[0].Exception).IsTypeOf<InvalidOperationException>();
        await Assert.That(batches).Count().IsEqualTo(2);
        await Assert.That(batches[1]).IsEquivalentTo([1L, 2L, 3L]);
        await Assert.That(batchStore.Marks).IsEquivalentTo([3L]);
    }

    [Test]
    public async Task ABatchDispatcherDispatchesASingleEventAsABatchOfOne()
    {
        var batches = new List<IReadOnlyList<DispatchedEvent>>();
        IEventDispatcher dispatcher = new BatchDispatcher(batches.Add);
        var metadata = new Dictionary<string, string> { ["tenant"] = "a" };
        var @event = new Event { Id = 1 };

        await dispatcher.DispatchAsync(7, metadata, @event, CancellationToken.None);

        await Assert.That(batches).HasSingleItem();
        await Assert.That(batches[0]).IsEquivalentTo([new DispatchedEvent(7, metadata, @event)]);
    }

    private static async Task<RecordingBatchStore> CreateBatchStoreAsync(int events)
    {
        var eventStore = new MemoryEventStore();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), Enumerable.Range(1, events).Select(static id => (object)new Event { Id = id }).ToArray(), Guid.NewGuid(), null);
        return new RecordingBatchStore(new MemoryEventBatchStore(eventStore));
    }

    // Runs the host until the store has been marked up to the sequence number, then stops it.
    private static async Task<List<DispatchFailedEventArgs>> RunUntilMarkedAsync(IEventDispatcher eventDispatcher, RecordingBatchStore batchStore, long sequenceNumber)
    {
        var failures = new List<DispatchFailedEventArgs>();
        using var stopping = new CancellationTokenSource();
        var dispatcher = new Sdk.EventDispatcher(eventDispatcher, batchStore, Options);
        dispatcher.DispatchFailed += (_, e) => failures.Add(e);
        batchStore.Marked += marked =>
        {
            if (marked >= sequenceNumber)
            {
                stopping.Cancel();
            }
        };

        await dispatcher.RunAsync(stopping.Token).WaitAsync(TimeSpan.FromSeconds(10));
        return failures;
    }

    private sealed class RecordingBatchStore(IEventBatchStore inner) : IEventBatchStore
    {
        public event Action<long>? Marked;

        public List<long> Marks { get; } = [];

        public Task<EventBatch?> GetNextBatchAsync(Guid dispatcherId, int batchSize, TimeSpan batchTimeout, CancellationToken cancellationToken = default) =>
            inner.GetNextBatchAsync(dispatcherId, batchSize, batchTimeout, cancellationToken);

        public async Task MarkDispatchedAsync(Guid dispatcherId, long sequenceNumber, CancellationToken cancellationToken = default)
        {
            await inner.MarkDispatchedAsync(dispatcherId, sequenceNumber, cancellationToken);
            this.Marks.Add(sequenceNumber);
            this.Marked?.Invoke(sequenceNumber);
        }
    }

    private sealed class BatchDispatcher(Action<IReadOnlyList<DispatchedEvent>> dispatch) : IEventBatchDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<DispatchedEvent> events, CancellationToken cancellationToken)
        {
            dispatch(events);
            return Task.CompletedTask;
        }
    }

    private sealed class Event
    {
        public int Id { get; set; }
    }
}
