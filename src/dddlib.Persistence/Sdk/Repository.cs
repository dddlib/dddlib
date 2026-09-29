using System.Globalization;
using dddlib.Runtime;
using dddlib.Sdk;

namespace dddlib.Persistence.Sdk;

/// <summary>
/// The memento-based repository: saves the memento of an aggregate root under its stream identity and
/// reconstitutes an aggregate root from that memento. Derive from it to supply the storage.
/// </summary>
public abstract class Repository<T> : IRepository<T>
    where T : AggregateRoot
{
    private readonly IIdentityMap identityMap;

    protected Repository(IIdentityMap identityMap)
    {
        ArgumentNullException.ThrowIfNull(identityMap);

        this.identityMap = identityMap;
    }

    public async Task SaveAsync(T aggregateRoot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        try
        {
            await this.SaveInternalAsync(aggregateRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (RuntimeException ex)
        {
            throw new PersistenceException(string.Concat("An exception occurred during the save operation.\r\n", ex.Message), ex);
        }
    }

    public async Task<T> LoadAsync(object naturalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(naturalKey);

        try
        {
            return await this.LoadInternalAsync(naturalKey, cancellationToken).ConfigureAwait(false);
        }
        catch (RuntimeException ex)
        {
            throw new PersistenceException(string.Concat("An exception occurred during the load operation.\r\n", ex.Message), ex);
        }
    }

    /// <summary>
    /// Stores the memento under the identity and returns the new state token. <paramref name="preCommitState"/>
    /// is null for a new aggregate root; a mismatch with the stored state is a <see cref="ConcurrencyException"/>.
    /// </summary>
    protected abstract Task<string> SaveAsync(Guid id, object memento, string? preCommitState, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the memento stored under the identity, or null if there is none.
    /// </summary>
    protected abstract Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken);

    private async Task SaveInternalAsync(T aggregateRoot, CancellationToken cancellationToken)
    {
        var type = aggregateRoot.GetType();
        var runtimeType = Application.Current.GetAggregateRootType(type);
        runtimeType.ValidateForPersistence();

        var preCommitState = aggregateRoot.State;
        var naturalKey = runtimeType.GetNaturalKey(aggregateRoot);
        var naturalKeyType = runtimeType.NaturalKey!.PropertyType;

        Guid id;
        if (preCommitState is null)
        {
            id = await this.identityMap.GetOrAddAsync(runtimeType.RuntimeType, naturalKeyType, naturalKey, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            id = await this.identityMap.TryGetAsync(runtimeType.RuntimeType, naturalKeyType, naturalKey, cancellationToken).ConfigureAwait(false)
                ?? throw new ConcurrencyException("Aggregate root does not exist.");
        }

        var memento = aggregateRoot.GetMemento()
            ?? throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The aggregate root of type '{0}' has not been configured to return a memento representing its state.
To fix this issue:
- override the 'GetState' method of the aggregate root to return a memento describing its state.",
                    type))
            {
                HelpLink = "https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-mementos.md",
            };

        var postCommitState = await this.SaveAsync(id, memento, preCommitState, cancellationToken).ConfigureAwait(false);
        aggregateRoot.CommitEvents(postCommitState);

        if (aggregateRoot.IsDestroyed)
        {
            await this.identityMap.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<T> LoadInternalAsync(object naturalKey, CancellationToken cancellationToken)
    {
        var runtimeType = Application.Current.GetAggregateRootType(typeof(T));
        runtimeType.ValidateForPersistence();
        runtimeType.ValidateNaturalKey(naturalKey);

        var id = await this.identityMap.TryGetAsync(runtimeType.RuntimeType, runtimeType.NaturalKey!.PropertyType, naturalKey, cancellationToken).ConfigureAwait(false);
        if (id is null)
        {
            runtimeType.ThrowNotFound(naturalKey);
        }

        var result = await this.LoadAsync(id.Value, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            runtimeType.ThrowNotFound(naturalKey);
        }

        var aggregateRoot = AggregateRootFactory.Create<T>(result.Memento, 0, [], result.State);
        if (aggregateRoot.IsDestroyed)
        {
            await this.identityMap.RemoveAsync(id.Value, cancellationToken).ConfigureAwait(false);
            runtimeType.ThrowNotFound(naturalKey);
        }

        return aggregateRoot;
    }
}
