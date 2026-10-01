namespace dddlib.Persistence.Projections;

/// <summary>
/// A key/value store of read model views, written by a projection and read by whoever serves the read model.
/// </summary>
/// <typeparam name="TIdentity">The type of the identity a view is keyed by.</typeparam>
/// <typeparam name="TEntity">The type of the view.</typeparam>
public interface IRepository<TIdentity, TEntity>
    where TIdentity : notnull
    where TEntity : class
{
    /// <summary>
    /// Gets the view with the specified identity, or null when there is none.
    /// </summary>
    Task<TEntity?> GetAsync(TIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every view with its identity.
    /// </summary>
    IAsyncEnumerable<KeyValuePair<TIdentity, TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the view, or replaces the one with the same identity.
    /// </summary>
    Task AddOrUpdateAsync(TIdentity identity, TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the view with the specified identity, if there is one.
    /// </summary>
    Task RemoveAsync(TIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every view.
    /// </summary>
    Task PurgeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or replaces the views in <paramref name="addOrUpdate"/> and removes the identities in
    /// <paramref name="remove"/>, in that order.
    /// </summary>
    Task BulkUpdateAsync(IEnumerable<KeyValuePair<TIdentity, TEntity>> addOrUpdate, IEnumerable<TIdentity> remove, CancellationToken cancellationToken = default);
}
