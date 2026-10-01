using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Projections.Sdk;

/// <summary>
/// What the <see cref="ProjectionRunner"/> drives: a projection together with where its checkpoint and its read model
/// are kept. A page of events and the checkpoint that follows it are applied in one unit, so that a crash or a lost
/// race between two runners never applies an event twice or loses one.
/// </summary>
public interface IProjectionStore
{
    /// <summary>
    /// Gets the name of the projection.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the event types the projection handles, which the runner asks the feed for, or null for every event.
    /// </summary>
    IReadOnlyCollection<Type>? EventTypes { get; }

    /// <summary>
    /// Gets the sequence number of the last event the projection has applied, or zero.
    /// </summary>
    Task<long> GetCheckpointAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the page's events and moves the checkpoint from <paramref name="expectedCheckpoint"/> to the page's
    /// end, atomically. Throws <see cref="ConcurrencyException"/>, having changed nothing, when the stored checkpoint
    /// is not the expected one: another runner applied the page first.
    /// </summary>
    Task ApplyAsync(EventPage page, long expectedCheckpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the read model and resets the checkpoint to zero, atomically, so that the projection is rebuilt from
    /// the first event.
    /// </summary>
    Task PurgeAsync(CancellationToken cancellationToken = default);
}
