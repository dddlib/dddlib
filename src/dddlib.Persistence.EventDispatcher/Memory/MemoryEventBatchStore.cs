using dddlib.Persistence.EventDispatcher.Sdk;
using dddlib.Persistence.Memory;

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
            state.Incomplete.RemoveAll(batch => now - batch.Timestamp >= batchTimeout);

            var after = Math.Max(state.Dispatched, state.Incomplete.Count == 0 ? 0 : state.Incomplete.Max(batch => batch.Last));
            var events = this.eventStore.ReadEvents(after, batchSize);
            if (events.Count == 0)
            {
                return Task.FromResult<EventBatch?>(null);
            }

            var id = ++this.nextBatchId;
            state.Incomplete.Add(new PendingBatch(id, events[^1].SequenceNumber, now));

            return Task.FromResult<EventBatch?>(new EventBatch(id, events));
        }
    }

    public Task MarkDispatchedAsync(Guid dispatcherId, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var state = this.GetState(dispatcherId);
            state.Dispatched = Math.Max(state.Dispatched, sequenceNumber);
            state.Incomplete.RemoveAll(batch => batch.Last <= sequenceNumber);
        }

        return Task.CompletedTask;
    }

    private DispatcherState GetState(Guid dispatcherId)
    {
        if (!this.dispatchers.TryGetValue(dispatcherId, out var state))
        {
            state = new DispatcherState();
            this.dispatchers.Add(dispatcherId, state);
        }

        return state;
    }

    private sealed record PendingBatch(long Id, long Last, DateTimeOffset Timestamp);

    private sealed class DispatcherState
    {
        public long Dispatched { get; set; }

        public List<PendingBatch> Incomplete { get; } = [];
    }
}
