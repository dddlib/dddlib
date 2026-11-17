namespace dddlib.Persistence.Sdk;

/// <summary>
/// Thrown by <see cref="IMultiStreamEventStore.CommitStreamsAsync"/> when a stream's state is not the state the commit
/// expected. <see cref="StreamId"/> names the stream, so that the caller can tell which of its aggregate roots failed.
/// </summary>
public class StreamConcurrencyException : ConcurrencyException
{
    public StreamConcurrencyException(Guid streamId, string? message)
        : this(streamId, message, null)
    {
    }

    public StreamConcurrencyException(Guid streamId, string? message, Exception? innerException)
        : base(message, innerException)
    {
        this.StreamId = streamId;
    }

    public Guid StreamId { get; }

    /// <summary>
    /// Checks the commits of <see cref="IMultiStreamEventStore.CommitStreamsAsync"/>: no null commit, at least one event
    /// per commit, and no stream twice.
    /// </summary>
    internal static void Validate(IReadOnlyList<StreamCommit> commits)
    {
        ArgumentNullException.ThrowIfNull(commits);

        var streamIds = new HashSet<Guid>();
        foreach (var commit in commits)
        {
            if (commit is null)
            {
                throw new ArgumentException("The commits contain a null commit.", nameof(commits));
            }

            if (commit.Events is null || commit.Events.Count == 0)
            {
                throw new ArgumentException($"The commit to stream '{commit.StreamId}' has no events.", nameof(commits));
            }

            if (!streamIds.Add(commit.StreamId))
            {
                throw new ArgumentException($"The commits contain stream '{commit.StreamId}' more than once.", nameof(commits));
            }
        }
    }
}
