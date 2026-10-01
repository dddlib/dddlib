namespace dddlib.Persistence.Projections;

/// <summary>
/// Raised by a <see cref="ProjectionRunner"/> when an iteration fails. A handler failure is a
/// <see cref="ProjectionException"/> naming the sequence number and the event; anything else is a problem reading the
/// feed or the store.
/// </summary>
public sealed class ProjectionFailedEventArgs(string projectionName, Exception exception) : EventArgs
{
    public string ProjectionName { get; } = projectionName;

    public Exception Exception { get; } = exception;
}
