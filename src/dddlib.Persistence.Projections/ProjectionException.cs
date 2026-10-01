using System.Globalization;

namespace dddlib.Persistence.Projections;

/// <summary>
/// Thrown when a projection's handler fails: names the projection, the sequence number and the event, with the
/// handler's exception as the inner exception.
/// </summary>
public class ProjectionException : PersistenceException
{
    public ProjectionException()
        : this("A projection exception has occurred.", null)
    {
    }

    public ProjectionException(string? message)
        : this(message, null)
    {
    }

    public ProjectionException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public ProjectionException(string projectionName, long sequenceNumber, object @event, Exception innerException)
        : base(
            string.Format(
                CultureInfo.InvariantCulture,
                "The projection '{0}' failed to apply the event '{1}' at sequence number {2}: {3}",
                projectionName,
                @event?.GetType().Name,
                sequenceNumber,
                innerException?.Message),
            innerException)
    {
        this.ProjectionName = projectionName;
        this.SequenceNumber = sequenceNumber;
        this.Event = @event;
    }

    /// <summary>
    /// Gets the name of the projection that failed.
    /// </summary>
    public string? ProjectionName { get; }

    /// <summary>
    /// Gets the sequence number of the event that could not be applied, or zero when the failure is not tied to one.
    /// </summary>
    public long SequenceNumber { get; }

    /// <summary>
    /// Gets the event that could not be applied, if the failure is tied to one.
    /// </summary>
    public object? Event { get; }
}
