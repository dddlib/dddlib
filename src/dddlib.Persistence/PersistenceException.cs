namespace dddlib.Persistence;

/// <summary>
/// Represents a persistence exception: the repository could not complete a save or load operation.
/// </summary>
public class PersistenceException : Exception
{
    public PersistenceException()
        : this("A persistence exception has occurred.", null)
    {
    }

    public PersistenceException(string? message)
        : this(message, null)
    {
    }

    public PersistenceException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
