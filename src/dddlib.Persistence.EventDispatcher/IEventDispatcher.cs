namespace dddlib.Persistence.EventDispatcher;

/// <summary>
/// Receives the persisted events, in sequence order within each batch. A throwing dispatcher stops the current batch;
/// the undispatched events are delivered again once the batch times out, so implementations must tolerate
/// at-least-once delivery. A batch that a host took and did not finish, because it stopped or because another host
/// sharing the dispatcher id did, is delivered again after later batches. Do not skip an event because its sequence
/// number is below one already handled.
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
