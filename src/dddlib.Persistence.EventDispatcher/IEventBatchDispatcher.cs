using dddlib.Persistence.EventDispatcher.Sdk;

namespace dddlib.Persistence.EventDispatcher;

/// <summary>
/// Receives each batch of persisted events whole, in sequence order, so it can deliver them in one round trip, for
/// example as one publish to a bus. The host treats the batch as all or nothing: when this throws, no event of the
/// batch is marked dispatched and the whole batch is delivered again once it times out, so implementations must
/// tolerate at-least-once delivery.
/// </summary>
public interface IEventBatchDispatcher : IEventDispatcher
{
    /// <summary>
    /// Dispatches the events, which are consecutive and in sequence order.
    /// </summary>
    Task DispatchAsync(IReadOnlyList<DispatchedEvent> events, CancellationToken cancellationToken);

    /// <summary>
    /// Dispatches the event as a batch of one.
    /// </summary>
    Task IEventDispatcher.DispatchAsync(long sequenceNumber, IReadOnlyDictionary<string, string> metadata, object @event, CancellationToken cancellationToken) =>
        this.DispatchAsync([new DispatchedEvent(sequenceNumber, metadata, @event)], cancellationToken);
}
