namespace dddlib.Persistence;

/// <summary>
/// Persists aggregate roots as mementos (their current state) rather than as event streams.
/// </summary>
public interface IRepository<T>
    where T : AggregateRoot
{
    Task SaveAsync(T aggregateRoot, CancellationToken cancellationToken = default);

    Task<T> LoadAsync(object naturalKey, CancellationToken cancellationToken = default);
}
