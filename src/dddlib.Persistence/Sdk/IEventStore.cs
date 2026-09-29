namespace dddlib.Persistence.Sdk;

/// <summary>
/// Stores event streams. The state token returned by a commit must be presented on the next commit to the same
/// stream; a mismatch means another commit happened in between and raises a <see cref="ConcurrencyException"/>.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Gets the events of a stream from the specified revision onwards, together with the current state token.
    /// An unknown stream yields no events and a null state.
    /// </summary>
    Task<StreamResult> GetStreamAsync(Guid streamId, int streamRevision, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends events to a stream and returns the new state token. <paramref name="preCommitState"/> is null for
    /// the first commit to a stream.
    /// </summary>
    Task<string> CommitStreamAsync(
        Guid streamId,
        IReadOnlyList<object> events,
        Guid correlationId,
        string? preCommitState,
        CancellationToken cancellationToken = default);
}
