namespace dddlib.Persistence.Projections;

/// <summary>
/// Where a projection is relative to the event store, for a health check or a dashboard.
/// </summary>
/// <param name="Name">The name of the projection.</param>
/// <param name="Checkpoint">The sequence number of the last event the projection has applied.</param>
/// <param name="LastSequenceNumber">The sequence number of the last event in the store.</param>
public sealed record ProjectionStatus(string Name, long Checkpoint, long LastSequenceNumber)
{
    /// <summary>
    /// Gets how many sequence numbers the projection is behind the store. Gaps left by rolled-back commits count, so
    /// it is an upper bound on the events still to apply, and zero means caught up.
    /// </summary>
    public long Lag => Math.Max(0, this.LastSequenceNumber - this.Checkpoint);
}
