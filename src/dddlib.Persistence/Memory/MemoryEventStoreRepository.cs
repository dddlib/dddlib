using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Memory;

/// <summary>
/// An event store repository backed entirely by in-process stores. Nothing is shared between instances.
/// </summary>
public sealed class MemoryEventStoreRepository : EventStoreRepository
{
    public MemoryEventStoreRepository()
        : base(new MemoryIdentityMap(), new MemoryEventStore(), new MemorySnapshotStore())
    {
    }
}
