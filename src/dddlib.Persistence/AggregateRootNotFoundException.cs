namespace dddlib.Persistence;

/// <summary>
/// Thrown when no aggregate root exists for the specified natural key, or when it existed but its lifecycle has ended.
/// </summary>
public class AggregateRootNotFoundException : PersistenceException
{
    public AggregateRootNotFoundException()
        : this("Cannot find the aggregate root.", null)
    {
    }

    public AggregateRootNotFoundException(string? message)
        : this(message, null)
    {
    }

    public AggregateRootNotFoundException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
