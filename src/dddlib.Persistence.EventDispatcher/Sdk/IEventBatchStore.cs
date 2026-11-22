using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.EventDispatcher.Sdk;

/// <summary>
/// Hands out batches of undispatched events per dispatcher and records what has been dispatched.
/// </summary>
public interface IEventBatchStore
{
    /// <summary>
    /// Prepares the next batch of undispatched events for the dispatcher, one batch in flight per dispatcher id so that
    /// events are dispatched in sequence order. Returns null while a batch is open and was handed out less than
    /// <paramref name="batchTimeout"/> ago and not released. An open batch past that comes back, under a new id, with
    /// the events not yet marked dispatched. Otherwise a new batch holds up to <paramref name="batchSize"/> events after
    /// every batch already handed out. Returns null when there is nothing to dispatch.
    /// </summary>
    Task<EventBatch?> GetNextBatchAsync(Guid dispatcherId, int batchSize, TimeSpan batchTimeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the events of the batch holding the sequence number have been dispatched up to and including it,
    /// completing that batch, and no other, when it is its last event. The dispatcher's position moves over the batches
    /// completed after it, never past one that is still open, so the events of a batch that a host took and did not
    /// finish are handed out again after the timeout or a release.
    /// </summary>
    Task MarkDispatchedAsync(Guid dispatcherId, long sequenceNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives up a batch that is not finished, so that its events not yet marked dispatched are handed out again at once
    /// rather than after the batch timeout. The host calls it when it stops partway through a batch, after marking the
    /// events it dispatched. A batch that is complete, or that has timed out and been handed out again since, is left
    /// as it is. A store that does not implement it leaves the batch to time out.
    /// </summary>
    Task ReleaseAsync(Guid dispatcherId, long batchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
