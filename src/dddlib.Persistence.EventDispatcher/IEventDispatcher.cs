namespace dddlib.Persistence.EventDispatcher;

/// <summary>
/// Receives the persisted events in sequence order. A throwing dispatcher stops the current batch; the undispatched
/// events are delivered again once the batch times out, so implementations must tolerate at-least-once delivery.
/// </summary>
public interface IEventDispatcher
{
    Task DispatchAsync(long sequenceNumber, object @event, CancellationToken cancellationToken);
}
