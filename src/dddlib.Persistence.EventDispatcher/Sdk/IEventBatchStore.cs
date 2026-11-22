using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.EventDispatcher.Sdk;

/// <summary>
/// Hands out batches of undispatched events per dispatcher and records what has been dispatched.
/// </summary>
public interface IEventBatchStore
{
    /// <summary>
    /// Prepares the next batch of undispatched events for the dispatcher. An open batch handed out more than
    /// <paramref name="batchTimeout"/> ago is handed out again first, earliest first, with the events not yet marked
    /// dispatched. Otherwise a new batch holds up to <paramref name="batchSize"/> events after every batch already
    /// handed out, so batches never overlap and dispatchers sharing an id take different events. Returns null when
    /// there is nothing to dispatch.
    /// </summary>
    Task<EventBatch?> GetNextBatchAsync(Guid dispatcherId, int batchSize, TimeSpan batchTimeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the events of the batch holding the sequence number have been dispatched up to and including it,
    /// completing that batch, and no other, when it is its last event. The dispatcher's position moves over the batches
    /// completed after it, never past one that is still open, so the events of a batch that another dispatcher with the
    /// same id took and did not finish are handed out again after the timeout.
    /// </summary>
    Task MarkDispatchedAsync(Guid dispatcherId, long sequenceNumber, CancellationToken cancellationToken = default);
}
