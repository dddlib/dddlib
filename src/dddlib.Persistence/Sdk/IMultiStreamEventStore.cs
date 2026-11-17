namespace dddlib.Persistence.Sdk;

/// <summary>
/// An event store that commits to several streams in one transaction: every stream's events are appended, or none
/// are. A separate interface from <see cref="IEventStore"/>, so that event stores that commit one stream at a time
/// keep working; <see cref="EventStoreRepository"/> needs it only to save several aggregate roots at once.
/// </summary>
public interface IMultiStreamEventStore : IEventStore
{
    /// <summary>
    /// Appends the events of each commit to its stream and returns the new state token of each stream, in the order of
    /// the commits. The events of the commits are given contiguous sequence numbers in that order. When a stream's
    /// state is not the commit's <see cref="StreamCommit.PreCommitState"/>, nothing is written and a
    /// <see cref="StreamConcurrencyException"/> names the first such stream. Each stream may appear once, and each
    /// commit must have at least one event.
    /// </summary>
    Task<IReadOnlyList<string>> CommitStreamsAsync(
        IReadOnlyList<StreamCommit> commits,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}
