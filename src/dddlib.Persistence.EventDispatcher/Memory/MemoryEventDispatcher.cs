using dddlib.Persistence.EventDispatcher.Sdk;
using dddlib.Persistence.Memory;

namespace dddlib.Persistence.EventDispatcher.Memory;

/// <summary>
/// An event dispatcher over the events committed to a <see cref="MemoryEventStore"/>: the same instance the
/// repository was composed with.
/// </summary>
public sealed class MemoryEventDispatcher : Sdk.EventDispatcher
{
    public MemoryEventDispatcher(MemoryEventStore eventStore, IEventDispatcher dispatcher, EventDispatcherOptions? options = null)
        : base(dispatcher, new MemoryEventBatchStore(eventStore), options)
    {
    }

    public MemoryEventDispatcher(MemoryEventStore eventStore, Action<long, object> dispatch, EventDispatcherOptions? options = null)
        : this(eventStore, new CustomEventDispatcher(dispatch), options)
    {
    }
}
