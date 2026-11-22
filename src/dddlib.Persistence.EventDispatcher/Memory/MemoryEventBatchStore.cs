using dddlib.Persistence.EventDispatcher.Sdk;
using dddlib.Persistence.Memory;
using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.EventDispatcher.Memory;

/// <summary>
/// Batches the events committed to a <see cref="MemoryEventStore"/>. Pass the same store instance the repository
/// writes to.
/// </summary>
public sealed class MemoryEventBatchStore : IEventBatchStore
{
    private readonly Lock sync = new();
    private readonly Dictionary<Guid, DispatcherState> dispatchers = [];
    private readonly MemoryEventStore eventStore;
    private readonly TimeProvider timeProvider;
    private long nextBatchId;

    public MemoryEventBatchStore(MemoryEventStore eventStore)
        : this(eventStore, TimeProvider.System)
    {
    }

    public MemoryEventBatchStore(MemoryEventStore eventStore, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.eventStore = eventStore;
        this.timeProvider = timeProvider;
    }

    public Task<EventBatch?> GetNextBatchAsync(Guid dispatcherId, int batchSize, TimeSpan batchTimeout, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var state = this.GetState(dispatcherId);
            var now = this.timeProvider.GetUtcNow();

            // An open batch that has timed out is handed out again, the earliest first, with the events it has left.
            var expired = state.Batches.Find(batch => !batch.Complete && now - batch.Timestamp >= batchTimeout);
            if (expired is not null)
            {
                expired.Timestamp = now;
                var after = Math.Max(state.Position, expired.Dispatched);
                var rest = this.eventStore.ReadEvents(after, expired.Count).Where(e => e.SequenceNumber <= expired.Last).ToArray();
                return Task.FromResult<EventBatch?>(ToBatch(expired.Id, rest));
            }

            // Otherwise the next batch starts after every batch handed out, so batches never overlap.
            var last = state.Batches.Count == 0 ? state.Position : Math.Max(state.Position, state.Batches[^1].Last);
            var events = this.eventStore.ReadEvents(last, batchSize);
            if (events.Count == 0)
            {
                return Task.FromResult<EventBatch?>(null);
            }

            var id = ++this.nextBatchId;
            state.Batches.Add(new PendingBatch(id, events[0].SequenceNumber, events[^1].SequenceNumber, events.Count) { Timestamp = now });
            return Task.FromResult<EventBatch?>(ToBatch(id, events));
        }
    }

    public Task MarkDispatchedAsync(Guid dispatcherId, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var state = this.GetState(dispatcherId);

            // Batches never overlap, so the open batch that holds the sequence number is the caller's.
            var batch = state.Batches.Find(batch => !batch.Complete && batch.First <= sequenceNumber && sequenceNumber <= batch.Last);
            if (batch is not null)
            {
                batch.Dispatched = Math.Max(batch.Dispatched, sequenceNumber);
                batch.Complete = batch.Dispatched >= batch.Last;
            }

            // The position moves over the completed batches that follow it and into a partly dispatched one, but never
            // past a batch that is still open.
            while (state.Batches.Count > 0 && state.Batches[0].Complete)
            {
                state.Position = Math.Max(state.Position, state.Batches[0].Last);
                state.Batches.RemoveAt(0);
            }

            if (state.Batches.Count > 0)
            {
                state.Position = Math.Max(state.Position, state.Batches[0].Dispatched);
            }
        }

        return Task.CompletedTask;
    }

    private static EventBatch ToBatch(long id, IReadOnlyList<FeedEvent> events) =>
        new(id, events.Select(static e => new DispatchedEvent(e.SequenceNumber, e.Metadata, e.Event)).ToArray());

    private DispatcherState GetState(Guid dispatcherId)
    {
        if (!this.dispatchers.TryGetValue(dispatcherId, out var state))
        {
            state = new DispatcherState();
            this.dispatchers.Add(dispatcherId, state);
        }

        return state;
    }

    // A batch from its first to its last event, of Count events when it was handed out, and the last of them
    // dispatched so far.
    private sealed class PendingBatch(long id, long first, long last, int count)
    {
        public long Id { get; } = id;

        public long First { get; } = first;

        public long Last { get; } = last;

        public int Count { get; } = count;

        public long Dispatched { get; set; } = first - 1;

        public bool Complete { get; set; }

        public DateTimeOffset Timestamp { get; set; }
    }

    private sealed class DispatcherState
    {
        // Every event up to here has been dispatched.
        public long Position { get; set; }

        // The batches past the position, in order.
        public List<PendingBatch> Batches { get; } = [];
    }
}