namespace dddlib.Runtime;

/// <summary>
/// Dispatches events to the handler methods of a target object.
/// </summary>
public interface IEventDispatcher
{
    void Dispatch(object target, object @event);
}
