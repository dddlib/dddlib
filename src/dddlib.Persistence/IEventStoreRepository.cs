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

    /// <summary>
    /// Saves the aggregate roots, of any types, in one transaction: the events of every aggregate root are committed,
    /// or none are and no aggregate root changes. Aggregate roots without uncommitted events are left out.
    /// </summary>
    Task SaveAsync(IReadOnlyCollection<AggregateRoot> aggregateRoots, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the aggregate roots as <see cref="SaveAsync(IReadOnlyCollection{AggregateRoot}, CancellationToken)"/> does,
    /// with one correlation id for every event.
    /// </summary>
    Task SaveAsync(IReadOnlyCollection<AggregateRoot> aggregateRoots, Guid correlationId, CancellationToken cancellationToken = default);

    Task<T> LoadAsync<T>(object naturalKey, CancellationToken cancellationToken = default)
        where T : AggregateRoot;
}
