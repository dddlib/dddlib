using System.Globalization;
using dddlib.Runtime;
using dddlib.Sdk;
using dddlib.Sdk.Configuration.Model;

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
    /// Adds the aggregate root to the batch, to be saved through this repository when the batch is saved. The memento
    /// and events are taken then, not now. A repository takes part only in a batch of its own storage: a batch of
    /// another kind is an <see cref="ArgumentException"/>, and a repository that cannot take part in batches throws a
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public void Add(IBatch batch, T aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        this.AddToBatch(batch, new BatchedSave(this, aggregateRoot));
    }

    /// <summary>
    /// Adds a save to a batch of this repository's storage. Only the batches dddlib ships can drive a save, so this is
    /// overridden by the repositories that ship with them.
    /// </summary>
    internal virtual void AddToBatch(IBatch batch, BatchSave save) =>
        throw new NotSupportedException(
            string.Format(
                CultureInfo.InvariantCulture,
                @"The repository of type '{0}' cannot save aggregate roots in a batch.
To fix this issue, either:
- save the aggregate root with the repository's 'SaveAsync' method, or
- use a repository that can take part in a batch, such as 'dddlib.Persistence.Memory.MemoryRepository<T>'.",
                this.GetType()));

    /// <summary>
    /// Stores the memento under the identity and returns the new state token. <paramref name="preCommitState"/>
    /// is null for a new aggregate root; a mismatch with the stored state is a <see cref="ConcurrencyException"/>.
    /// <paramref name="events"/> are the uncommitted events of the aggregate root, possibly none. Storing them in the
    /// same transaction as the memento lets the event dispatcher deliver them; they are never used for
    /// reconstitution, and an implementation that has no use for them may ignore them.
    /// </summary>
    protected abstract Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, string? preCommitState, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the memento stored under the identity, or null if there is none.
    /// </summary>
    protected abstract Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken);

    private static (AggregateRootType RuntimeType, object NaturalKey, object Memento, object[] Events) Prepare(AggregateRoot aggregateRoot)
    {
        var type = aggregateRoot.GetType();
        var runtimeType = Application.Current.GetAggregateRootType(type);
        runtimeType.ValidateForPersistence();

        var naturalKey = runtimeType.GetNaturalKey(aggregateRoot);
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

        return (runtimeType, naturalKey, memento, aggregateRoot.GetUncommittedEvents().ToArray());
    }

    // Returns null when an aggregate root that has been saved before no longer has an identity.
    private async Task<Guid?> ResolveIdentityAsync(AggregateRootType runtimeType, object naturalKey, string? preCommitState, CancellationToken cancellationToken)
    {
        var naturalKeyType = runtimeType.NaturalKey!.PropertyType;

        return preCommitState is null
            ? await this.identityMap.GetOrAddAsync(runtimeType.RuntimeType, naturalKeyType, naturalKey, cancellationToken).ConfigureAwait(false)
            : await this.identityMap.TryGetAsync(runtimeType.RuntimeType, naturalKeyType, naturalKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task CompleteAsync(AggregateRoot aggregateRoot, Guid id, CancellationToken cancellationToken)
    {
        if (aggregateRoot.IsDestroyed)
        {
            await this.identityMap.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SaveInternalAsync(T aggregateRoot, CancellationToken cancellationToken)
    {
        var (runtimeType, naturalKey, memento, events) = Prepare(aggregateRoot);
        var preCommitState = aggregateRoot.State;

        var id = await this.ResolveIdentityAsync(runtimeType, naturalKey, preCommitState, cancellationToken).ConfigureAwait(false)
            ?? throw new ConcurrencyException("Aggregate root does not exist.");

        var postCommitState = await this.SaveAsync(id, memento, events, preCommitState, cancellationToken).ConfigureAwait(false);
        aggregateRoot.CommitEvents(postCommitState);

        await this.CompleteAsync(aggregateRoot, id, cancellationToken).ConfigureAwait(false);
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

    private sealed class BatchedSave(Repository<T> repository, T aggregateRoot) : BatchSave(aggregateRoot)
    {
        public override void Prepare()
        {
            (this.RuntimeType, this.NaturalKey, this.Memento, var events) = Repository<T>.Prepare(aggregateRoot);
            this.Events = events;
            this.PreCommitState = aggregateRoot.State;
        }

        public override async Task ResolveIdentityAsync(CancellationToken cancellationToken) =>
            this.Id = await repository.ResolveIdentityAsync(this.RuntimeType, this.NaturalKey, this.PreCommitState, cancellationToken).ConfigureAwait(false)
                ?? throw this.Conflict("does not exist");

        public override Task CompleteAsync(CancellationToken cancellationToken) =>
            repository.CompleteAsync(aggregateRoot, this.Id, cancellationToken);
    }
}
