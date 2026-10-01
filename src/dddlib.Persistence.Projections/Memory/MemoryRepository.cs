using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace dddlib.Persistence.Projections.Memory;

/// <summary>
/// An in-process repository of views, for tests and for read models that need not survive a restart.
/// </summary>
/// <typeparam name="TIdentity">The type of the identity a view is keyed by.</typeparam>
/// <typeparam name="TEntity">The type of the view.</typeparam>
public class MemoryRepository<TIdentity, TEntity> : IRepository<TIdentity, TEntity>
    where TIdentity : notnull
    where TEntity : class
{
    private readonly ConcurrentDictionary<TIdentity, TEntity> entities;

    public MemoryRepository()
        : this(EqualityComparer<TIdentity>.Default)
    {
    }

    public MemoryRepository(IEqualityComparer<TIdentity> comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);

        this.Comparer = comparer;
        this.entities = new ConcurrentDictionary<TIdentity, TEntity>(comparer);
    }

    /// <summary>
    /// Gets the comparer identities are compared with.
    /// </summary>
    public IEqualityComparer<TIdentity> Comparer { get; }

    public virtual Task<TEntity?> GetAsync(TIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return Task.FromResult(this.entities.GetValueOrDefault(identity));
    }

    public virtual async IAsyncEnumerable<KeyValuePair<TIdentity, TEntity>> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);

        foreach (var entity in this.entities.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entity;
        }
    }

    public virtual Task AddOrUpdateAsync(TIdentity identity, TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(entity);

        this.entities[identity] = entity;
        return Task.CompletedTask;
    }

    public virtual Task RemoveAsync(TIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        this.entities.TryRemove(identity, out _);
        return Task.CompletedTask;
    }

    public virtual Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        this.entities.Clear();
        return Task.CompletedTask;
    }

    public virtual Task BulkUpdateAsync(IEnumerable<KeyValuePair<TIdentity, TEntity>> addOrUpdate, IEnumerable<TIdentity> remove, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addOrUpdate);
        ArgumentNullException.ThrowIfNull(remove);

        this.Apply(addOrUpdate.Select(static item => new KeyValuePair<TIdentity, TEntity?>(item.Key, item.Value)).Concat(remove.Select(static identity => new KeyValuePair<TIdentity, TEntity?>(identity, null))), purge: false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies a purge and a set of changes (a view to add or replace, or null to remove) directly, for the memory
    /// projection store's commit.
    /// </summary>
    internal void Apply(IEnumerable<KeyValuePair<TIdentity, TEntity?>> changes, bool purge)
    {
        if (purge)
        {
            this.entities.Clear();
        }

        foreach (var change in changes)
        {
            if (change.Value is null)
            {
                this.entities.TryRemove(change.Key, out _);
            }
            else
            {
                this.entities[change.Key] = change.Value;
            }
        }
    }
}
