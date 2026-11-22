namespace dddlib.Persistence.EventDispatcher;

/// <summary>
/// Receives the persisted events in sequence order. A throwing dispatcher stops the current batch; the undispatched
/// events are delivered again once the batch times out, before any later event, so implementations must tolerate
/// at-least-once delivery. After a crash, events dispatched but not yet marked are delivered again, in order, so an
/// event can arrive again after later ones of its batch.
/// </summary>
public interface IEventDispatcher
{
    /// <summary>
    /// Dispatches the event. <paramref name="sequenceNumber"/> is its position in the store-wide sequence of committed
    /// events, which has gaps where commits rolled back. <paramref name="metadata"/> is what the event was stored with:
    /// dddlib's <c>hostname</c> and <c>timestamp</c> of the save, and any entries the repository appended with the
    /// events.
    /// </summary>
    Task DispatchAsync(long sequenceNumber, IReadOnlyDictionary<string, string> metadata, object @event, CancellationToken cancellationToken);
}
