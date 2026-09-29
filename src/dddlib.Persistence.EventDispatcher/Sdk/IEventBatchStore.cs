using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.EventDispatcher.Sdk;

/// <summary>
/// Hands out batches of undispatched events per dispatcher and records what has been dispatched.
/// </summary>
public interface IEventBatchStore
{
    /// <summary>
    /// Prepares the next batch of undispatched events for the dispatcher: the events after the dispatcher's
    /// high-water mark and after any batch that is still in progress, up to <paramref name="batchSize"/> of them.
    /// Batches older than <paramref name="batchTimeout"/> that were never completed are handed out again.
    /// Returns null when there is nothing to dispatch.
    /// </summary>
    Task<EventBatch?> GetNextBatchAsync(Guid dispatcherId, int batchSize, TimeSpan batchTimeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that every event up to and including the sequence number has been dispatched, completing the batch
    /// it ends.
    /// </summary>
    Task MarkDispatchedAsync(Guid dispatcherId, long sequenceNumber, CancellationToken cancellationToken = default);
}
