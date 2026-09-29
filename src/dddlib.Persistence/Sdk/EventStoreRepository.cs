using System.Globalization;
using dddlib.Runtime;
using dddlib.Sdk;

namespace dddlib.Persistence.Sdk;

/// <summary>
/// The event store repository: saves the uncommitted events of an aggregate root to its stream and reconstitutes
/// an aggregate root from its latest snapshot plus the events that follow it.
/// </summary>
public class EventStoreRepository : IEventStoreRepository
{
    private readonly AggregateRootFactory factory = new();
    private readonly IIdentityMap identityMap;
    private readonly IEventStore eventStore;
    private readonly ISnapshotStore snapshotStore;

    public EventStoreRepository(IIdentityMap identityMap, IEventStore eventStore, ISnapshotStore snapshotStore)
    {
        ArgumentNullException.ThrowIfNull(identityMap);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(snapshotStore);

        this.identityMap = identityMap;
        this.eventStore = eventStore;
        this.snapshotStore = snapshotStore;
    }

    public Task SaveAsync<T>(T aggregateRoot, CancellationToken cancellationToken = default)
        where T : AggregateRoot
        => this.SaveAsync(aggregateRoot, Guid.NewGuid(), cancellationToken);

    public async Task SaveAsync<T>(T aggregateRoot, Guid correlationId, CancellationToken cancellationToken = default)
        where T : AggregateRoot
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        try
        {
            await this.SaveInternalAsync(aggregateRoot, correlationId, cancellationToken).ConfigureAwait(false);
        }
        catch (RuntimeException ex)
        {
            throw new PersistenceException(string.Concat("An exception occurred during the save operation.\r\n", ex.Message), ex);
        }
    }

    public async Task<T> LoadAsync<T>(object naturalKey, CancellationToken cancellationToken = default)
        where T : AggregateRoot
    {
        ArgumentNullException.ThrowIfNull(naturalKey);

        try
        {
            return await this.LoadInternalAsync<T>(naturalKey, cancellationToken).ConfigureAwait(false);
        }
        catch (RuntimeException ex)
        {
            throw new PersistenceException(string.Concat("An exception occurred during the load operation.\r\n", ex.Message), ex);
        }
    }

    private async Task SaveInternalAsync<T>(T aggregateRoot, Guid correlationId, CancellationToken cancellationToken)
        where T : AggregateRoot
    {
        var runtimeType = Application.Current.GetAggregateRootType(aggregateRoot.GetType());
        runtimeType.ValidateForPersistence();

        var preCommitState = aggregateRoot.State;
        var naturalKey = runtimeType.GetNaturalKey(aggregateRoot);
        var naturalKeyType = runtimeType.NaturalKey!.PropertyType;

        Guid streamId;
        if (preCommitState is null)
        {
            streamId = await this.identityMap.GetOrAddAsync(runtimeType.RuntimeType, naturalKeyType, naturalKey, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            streamId = await this.identityMap.TryGetAsync(runtimeType.RuntimeType, naturalKeyType, naturalKey, cancellationToken).ConfigureAwait(false)
                ?? throw new ConcurrencyException("Aggregate root does not exist.");
        }

        var events = aggregateRoot.GetUncommittedEvents().ToArray();
        if (preCommitState is null && events.Length == 0)
        {
            throw new PersistenceException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"Cannot save initial commit for aggregate root of type '{0}' as there are no events to save.
To fix this issue:
- ensure that your aggregate root is configured to use event application, and
- ensure that the events are getting applied using the base 'Apply' method.
Further information: https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Event-Application",
                    aggregateRoot.GetType()));
        }

        if (events.Length == 0)
        {
            return;
        }

        var postCommitState = await this.eventStore.CommitStreamAsync(streamId, events, correlationId, preCommitState, cancellationToken).ConfigureAwait(false);
        aggregateRoot.CommitEvents(postCommitState);

        if (aggregateRoot.IsDestroyed)
        {
            await this.identityMap.RemoveAsync(streamId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<T> LoadInternalAsync<T>(object naturalKey, CancellationToken cancellationToken)
        where T : AggregateRoot
    {
        var runtimeType = Application.Current.GetAggregateRootType(typeof(T));
        runtimeType.ValidateForPersistence();
        runtimeType.ValidateNaturalKey(naturalKey);

        var streamId = await this.identityMap.TryGetAsync(runtimeType.RuntimeType, runtimeType.NaturalKey!.PropertyType, naturalKey, cancellationToken).ConfigureAwait(false);
        if (streamId is null)
        {
            runtimeType.ThrowNotFound(naturalKey);
        }

        var snapshot = await this.snapshotStore.GetSnapshotAsync(streamId.Value, cancellationToken).ConfigureAwait(false) ?? new Snapshot(0, null);
        var stream = await this.eventStore.GetStreamAsync(streamId.Value, snapshot.StreamRevision, cancellationToken).ConfigureAwait(false);
        if (snapshot.StreamRevision == 0 && stream.Events.Count == 0)
        {
            runtimeType.ThrowNotFound(naturalKey);
        }

        var aggregateRoot = this.factory.Create<T>(snapshot.Memento, snapshot.StreamRevision, stream.Events, stream.State);
        if (aggregateRoot.IsDestroyed)
        {
            await this.identityMap.RemoveAsync(streamId.Value, cancellationToken).ConfigureAwait(false);
            runtimeType.ThrowNotFound(naturalKey);
        }

        return aggregateRoot;
    }
}
