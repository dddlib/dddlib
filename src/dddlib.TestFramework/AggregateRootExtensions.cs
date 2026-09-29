namespace dddlib.TestFramework;

/// <summary>
/// Exposes the internal persistence surface of an aggregate root to tests.
/// </summary>
public static class AggregateRootExtensions
{
    public static IReadOnlyList<object> GetUncommittedEvents(this AggregateRoot aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        return aggregateRoot.GetUncommittedEvents();
    }

    public static object? GetMemento(this AggregateRoot aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        return aggregateRoot.GetMemento();
    }

    public static int GetRevision(this AggregateRoot aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        return aggregateRoot.Revision;
    }
}
