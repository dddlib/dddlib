namespace dddlib.Persistence.EventDispatcher;

/// <summary>
/// Receives the persisted events in sequence order. A throwing dispatcher stops the current batch; the undispatched
/// events are delivered again once the batch times out, so implementations must tolerate at-least-once delivery.
/// </summary>
public interface IEventDispatcher
{
    /// <summary>
    /// Dispatches the event. <paramref name="metadata"/> is what the event was stored with: on SQL Server, the
    /// <c>hostname</c> and <c>timestamp</c> of the save and any entries the repository appended with the events; in
    /// memory, nothing.
    /// </summary>
    Task DispatchAsync(long sequenceNumber, IReadOnlyDictionary<string, string> metadata, object @event, CancellationToken cancellationToken);
}
