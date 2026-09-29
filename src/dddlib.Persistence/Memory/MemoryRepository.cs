using System.Globalization;
using System.Text.Json;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;

namespace dddlib.Persistence.Memory;

/// <summary>
/// An in-process memento repository. Mementos are stored as JSON and deserialized on load so that a loaded
/// aggregate root never shares state with the one that was saved.
/// </summary>
public class MemoryRepository<T> : Repository<T>
    where T : AggregateRoot
{
    private readonly Lock sync = new();
    private readonly Dictionary<Guid, StoredMemento> store = [];

    public MemoryRepository()
        : this(new MemoryIdentityMap())
    {
    }

    public MemoryRepository(IIdentityMap identityMap)
        : base(identityMap)
    {
    }

    protected override Task<string> SaveAsync(Guid id, object memento, string? preCommitState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memento);

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
            this.store[id] = new StoredMemento(memento.GetType(), JsonSerializer.Serialize(memento, memento.GetType(), JsonSerialization.Options), state);

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
