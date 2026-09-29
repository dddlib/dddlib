using dddlib.Runtime;
using dddlib.Sdk.Generated;

namespace dddlib.Sdk;

/// <summary>
/// Builds the event dispatcher for an aggregate root type: one dispatcher per level of the type hierarchy, generated
/// where the level is a partial type covered by the source generator and reflection-based otherwise.
/// </summary>
internal static class EventDispatchers
{
    public static IEventDispatcher ForAggregateRoot(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var dispatchers = type.GetTypeHierarchyUntil(typeof(AggregateRoot))
            .Select(static level => (IEventDispatcher?)GeneratedMetadata.TryGet<IGeneratedAggregateRootMetadata>(level)
                ?? DefaultEventDispatcher.ForDeclaredHandlers(level))
            .ToArray();

        return dispatchers.Length == 1 ? dispatchers[0] : new CompositeEventDispatcher(dispatchers);
    }

    private sealed class CompositeEventDispatcher(IEventDispatcher[] dispatchers) : IEventDispatcher
    {
        public void Dispatch(object target, object @event)
        {
            foreach (var dispatcher in dispatchers)
            {
                dispatcher.Dispatch(target, @event);
            }
        }
    }
}
