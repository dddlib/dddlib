namespace dddlib.Persistence;

/// <summary>
/// Thrown when a commit conflicts with the current state of the stream: another commit happened first, the
/// aggregate root no longer exists, or it already exists.
/// </summary>
public class ConcurrencyException : PersistenceException
{
    public ConcurrencyException()
        : this("A concurrency exception has occurred.", null)
    {
    }

    public ConcurrencyException(string? message)
        : this(message, null)
    {
    }

    public ConcurrencyException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
