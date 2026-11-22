namespace dddlib.Persistence;

/// <summary>
/// Persists aggregate roots as mementos (their current state) rather than as event streams.
/// </summary>
public interface IRepository<T>
    where T : AggregateRoot
{
    /// <summary>
    /// Saves the aggregate root, giving every event the save commits a new correlation id.
    /// </summary>
    Task SaveAsync(T aggregateRoot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the aggregate root as <see cref="SaveAsync(T, CancellationToken)"/> does, with
    /// <paramref name="correlationId"/> for every event the save commits, for example the id of the request or message
    /// being handled. An empty correlation id is an <see cref="ArgumentException"/>, and nothing is saved.
    /// </summary>
    Task SaveAsync(T aggregateRoot, Guid correlationId, CancellationToken cancellationToken = default);

    Task<T> LoadAsync(object naturalKey, CancellationToken cancellationToken = default);
}
