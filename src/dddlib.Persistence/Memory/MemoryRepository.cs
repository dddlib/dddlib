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

    /// <summary>
    /// Gets metadata that the events do not carry, such as a tenant, stored beside dddlib's own <c>hostname</c> and
    /// <c>timestamp</c> entries and given to the event dispatcher and the event feed with each event, as
    /// <c>SqlServerRepository&lt;T&gt;.EventMetadata</c> is. A key equal to either of dddlib's is an
    /// <see cref="ArgumentException"/>, and nothing is stored.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, string>? EventMetadata => null;

    Lock IMemoryBatchParticipant.Sync => this.sync;

    long IMemoryBatchParticipant.BatchOrder => this.batchOrder;

    MemoryEventStore IMemoryBatchParticipant.EventStore => this.eventStore;

    object IMemoryBatchParticipant.Stage(object memento) => Stage(memento);

    MemoryEventStore.StagedEvents IMemoryBatchParticipant.StageEvents(Guid id, IReadOnlyList<object> events) => this.StageEvents(id, events);

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
        var stagedEvents = this.StageEvents(id, events);

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

    /// <summary>
    /// Gets metadata of one event's own, such as a message ID, as <c>SqlServerRepository&lt;T&gt;.GetEventMetadata</c>
    /// does. It is called once for each event of a save, alone or in a <see cref="MemoryBatch"/>, before anything is
    /// stored, with the aggregate root's identity and the event's zero-based position among the events of the save. Its
    /// entries are merged over <see cref="EventMetadata"/>: an entry with the same key replaces that one for this event
    /// only. A key equal to either of dddlib's is an <see cref="ArgumentException"/>, and nothing is stored. Returns null
    /// by default, for no metadata of the event's own.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, string>? GetEventMetadata(Guid id, object @event, int index) => null;

    private const string DoesNotExist = "does not exist";

    private static StoredMemento Stage(object memento) =>
        new(memento.GetType(), JsonSerializer.Serialize(memento, memento.GetType(), JsonSerialization.Options), string.Empty);

    private MemoryEventStore.StagedEvents StageEvents(Guid id, IReadOnlyList<object> events) =>
        MemoryEventStore.Stage(events, this.EventMetadata, (@event, index) => this.GetEventMetadata(id, @event, index));

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
