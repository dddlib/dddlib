using System.Globalization;
using System.Text.Json;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;

namespace dddlib.Persistence.Memory;

/// <summary>
/// An in-process memento repository. Mementos are stored as JSON and deserialized on load so that a loaded
/// aggregate root never shares state with the one that was saved. The events of each save are appended to a
/// <see cref="MemoryEventStore"/> so that they can be dispatched.
/// </summary>
public class MemoryRepository<T> : Repository<T>
    where T : AggregateRoot
{
    private readonly Lock sync = new();
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

    protected override Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, string? preCommitState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memento);
        ArgumentNullException.ThrowIfNull(events);

        lock (this.sync)
        {
            if (this.store.TryGetValue(id, out var existing))
            {
                if (existing.State != preCommitState)
                {
                    throw new ConcurrencyException("Invalid state.");
                }
            }
            else if (preCommitState is not null)
            {
                throw new ConcurrencyException("Aggregate root does not exist.");
            }

            var state = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            var stored = new StoredMemento(memento.GetType(), JsonSerializer.Serialize(memento, memento.GetType(), JsonSerialization.Options), state);

            // The events go first: once they are in, the memento write below cannot fail, so a dispatcher never sees
            // events for a memento that was not saved. The stream takes the memento's state token.
            this.eventStore.AppendEvents(id, events, state);
            this.store[id] = stored;

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

    private sealed record StoredMemento(Type Type, string Payload, string State);
}
