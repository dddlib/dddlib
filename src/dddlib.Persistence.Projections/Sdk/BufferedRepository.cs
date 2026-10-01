using System.Runtime.CompilerServices;

namespace dddlib.Persistence.Projections.Sdk;

/// <summary>
/// The repository a projection's handlers write to while a page is applied: reads come from the buffer first and then
/// the store, a buffered removal hides the stored view, the last operation per identity wins, and a purge hides the
/// whole store. Nothing reaches the store until the page is committed, so a failed page changes nothing.
/// </summary>
public sealed class BufferedRepository<TIdentity, TEntity> : IRepository<TIdentity, TEntity>
    where TIdentity : notnull
    where TEntity : class
{
    private readonly IRepository<TIdentity, TEntity> store;
    private readonly Dictionary<TIdentity, TEntity?> changes;

    public BufferedRepository(IRepository<TIdentity, TEntity> store, IEqualityComparer<TIdentity>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        this.store = store;
        this.changes = new Dictionary<TIdentity, TEntity?>(comparer);
    }

    /// <summary>
    /// Gets a value indicating whether the store is to be purged before the changes are applied.
    /// </summary>
    public bool IsPurged { get; private set; }

    /// <summary>
    /// Gets the buffered changes: a view to add or replace, or null for an identity to remove.
    /// </summary>
    public IReadOnlyDictionary<TIdentity, TEntity?> Changes => this.changes;

    public Task<TEntity?> GetAsync(TIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (this.changes.TryGetValue(identity, out var buffered))
        {
            return Task.FromResult(buffered);
        }

        return this.IsPurged ? Task.FromResult<TEntity?>(null) : this.store.GetAsync(identity, cancellationToken);
    }

    public async IAsyncEnumerable<KeyValuePair<TIdentity, TEntity>> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var change in this.changes)
        {
            if (change.Value is not null)
            {
                yield return new KeyValuePair<TIdentity, TEntity>(change.Key, change.Value);
            }
        }

        if (this.IsPurged)
        {
            yield break;
        }

        await foreach (var stored in this.store.GetAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!this.changes.ContainsKey(stored.Key))
            {
                yield return stored;
            }
        }
    }

    public Task AddOrUpdateAsync(TIdentity identity, TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(entity);

        this.changes[identity] = entity;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(TIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (this.IsPurged)
        {
            this.changes.Remove(identity);
        }
        else
        {
            this.changes[identity] = null;
        }

        return Task.CompletedTask;
    }

    public Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        this.IsPurged = true;
        this.changes.Clear();
        return Task.CompletedTask;
    }

    public async Task BulkUpdateAsync(IEnumerable<KeyValuePair<TIdentity, TEntity>> addOrUpdate, IEnumerable<TIdentity> remove, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addOrUpdate);
        ArgumentNullException.ThrowIfNull(remove);

        foreach (var item in addOrUpdate)
        {
            await this.AddOrUpdateAsync(item.Key, item.Value, cancellationToken).ConfigureAwait(false);
        }

        foreach (var identity in remove)
        {
            await this.RemoveAsync(identity, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Applies the purge, if any, and then the changes to the store, and empties the buffer.
    /// </summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (this.IsPurged)
        {
            await this.store.PurgeAsync(cancellationToken).ConfigureAwait(false);
        }

        if (this.changes.Count > 0)
        {
            var addOrUpdate = this.changes.Where(static change => change.Value is not null).Select(static change => new KeyValuePair<TIdentity, TEntity>(change.Key, change.Value!)).ToList();
            var remove = this.changes.Where(static change => change.Value is null).Select(static change => change.Key).ToList();
            await this.store.BulkUpdateAsync(addOrUpdate, remove, cancellationToken).ConfigureAwait(false);
        }

        this.IsPurged = false;
        this.changes.Clear();
    }
}
