namespace dddlib.Persistence;

/// <summary>
/// Persists aggregate roots as event streams and reconstitutes them from those streams (and snapshots).
/// </summary>
public interface IEventStoreRepository
{
    Task SaveAsync<T>(T aggregateRoot, CancellationToken cancellationToken = default)
        where T : AggregateRoot;

    Task SaveAsync<T>(T aggregateRoot, Guid correlationId, CancellationToken cancellationToken = default)
        where T : AggregateRoot;

    Task<T> LoadAsync<T>(object naturalKey, CancellationToken cancellationToken = default)
        where T : AggregateRoot;
}
