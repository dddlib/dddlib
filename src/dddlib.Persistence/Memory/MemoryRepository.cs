using System.Globalization;
using System.Text.Json;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;

namespace dddlib.Persistence.Memory;

/// <summary>
/// An in-process memento repository. Mementos are stored as JSON and deserialized on load so that a loaded
/// aggregate root never shares state with the one that was saved. The events of each save are appended to a
/// <see cref="MemoryEventStore"/> so that they can be dispatched. It takes part in a <see cref="MemoryBatch"/>.
/// </summary>
public class MemoryRepository<T> : Repository<T>, IMemoryBatchParticipant
    where T : AggregateRoot
{
    private static long instances;

    private readonly Lock sync = new();
    private readonly long batchOrder = Interlocked.Increment(ref instances);
    private readonly Dictionary<Guid, StoredMemento> store = [];
    private readonly MemoryEventStore eventStore;

    public MemoryRepository()
        : this(new MemoryIdentityMap(), new MemoryEventStore())
    {
    }

    public MemoryRepository(IIdentityMap identityMap)
        : this(identityMap, new MemoryEventStore())
    {
    }

    /// <summary>
    /// Creates a repository that appends the events of each save to <paramref name="eventStore"/>. Compose an event
    /// dispatcher with the same store instance to have them dispatched.
    /// </summary>
    public MemoryRepository(IIdentityMap identityMap, MemoryEventStore eventStore)
        : base(identityMap)
    {
        ArgumentNullException.ThrowIfNull(eventStore);

        this.eventStore = eventStore;
    }

    Lock IMemoryBatchParticipant.Sync => this.sync;

    long IMemoryBatchParticipant.BatchOrder => this.batchOrder;

    MemoryEventStore IMemoryBatchParticipant.EventStore => this.eventStore;

    object IMemoryBatchParticipant.Stage(object memento) => Stage(memento);

    string? IMemoryBatchParticipant.FindConflict(Guid id, string? preCommitState) => this.FindConflict(id, preCommitState);

    void IMemoryBatchParticipant.Store(Guid id, object staged, string state) => this.store[id] = ((StoredMemento)staged) with { State = state };

    internal override void AddToBatch(IBatch batch, BatchSave save)
    {
        if (batch is not MemoryBatch memoryBatch)
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "A memory repository saves aggregate roots only in a '{0}'.", typeof(MemoryBatch)),
                nameof(batch));
        }

        memoryBatch.Add(save, this);
    }

    protected override Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, string? preCommitState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memento);
        ArgumentNullException.ThrowIfNull(events);

        // Everything that can fail is done before anything is stored.
        var staged = Stage(memento);
        var stagedEvents = MemoryEventStore.Stage(events);

        lock (this.sync)
        {
            var conflict = this.FindConflict(id, preCommitState);
            if (conflict is not null)
            {
                throw new ConcurrencyException(conflict == DoesNotExist ? "Aggregate root does not exist." : "Invalid state.");
            }

            // The stream takes the memento's state token. The event store's lock is taken inside this repository's, as
            // a MemoryBatch takes them.
            var state = MemoryBatch.NewState();
            lock (this.eventStore.Sync)
            {
                this.eventStore.AppendStaged(id, stagedEvents, Guid.NewGuid(), state);
            }

            this.store[id] = staged with { State = state };

            return Task.FromResult(state);
        }
    }

    protected override Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (this.sync)
        {
            if (!this.store.TryGetValue(id, out var stored))
            {
                return Task.FromResult<MementoResult?>(null);
            }

            var memento = JsonSerializer.Deserialize(stored.Payload, stored.Type, JsonSerialization.Options)!;
            return Task.FromResult<MementoResult?>(new MementoResult(memento, stored.State));
        }
    }

    private const string DoesNotExist = "does not exist";

    private static StoredMemento Stage(object memento) =>
        new(memento.GetType(), JsonSerializer.Serialize(memento, memento.GetType(), JsonSerialization.Options), string.Empty);

    // The caller holds the lock.
    private string? FindConflict(Guid id, string? preCommitState)
    {
        if (!this.store.TryGetValue(id, out var existing))
        {
            return preCommitState is null ? null : DoesNotExist;
        }

        if (existing.State == preCommitState)
        {
            return null;
        }

        return preCommitState is null ? "already exists" : "has changed since it was loaded";
    }

    private sealed record StoredMemento(Type Type, string Payload, string State);
}
