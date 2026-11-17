using System.Globalization;
using dddlib.Runtime;
using dddlib.Sdk;
using dddlib.Sdk.Configuration.Model;

namespace dddlib.Persistence.Sdk;

/// <summary>
/// The event store repository: saves the uncommitted events of an aggregate root to its stream and reconstitutes
/// an aggregate root from its latest snapshot plus the events that follow it.
/// </summary>
public class EventStoreRepository : IEventStoreRepository
{
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

    public Task SaveAsync(IReadOnlyCollection<AggregateRoot> aggregateRoots, CancellationToken cancellationToken = default)
        => this.SaveAsync(aggregateRoots, Guid.NewGuid(), cancellationToken);

    public async Task SaveAsync(IReadOnlyCollection<AggregateRoot> aggregateRoots, Guid correlationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoots);

        var distinct = new HashSet<AggregateRoot>(ReferenceEqualityComparer.Instance);
        foreach (var aggregateRoot in aggregateRoots)
        {
            if (aggregateRoot is null)
            {
                throw new ArgumentException("The aggregate roots contain a null aggregate root.", nameof(aggregateRoots));
            }

            if (!distinct.Add(aggregateRoot))
            {
                throw new ArgumentException("The aggregate roots contain the same aggregate root more than once.", nameof(aggregateRoots));
            }
        }

        if (aggregateRoots.Count == 0)
        {
            return;
        }

        if (this.eventStore is not IMultiStreamEventStore multiStreamEventStore)
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"Cannot save several aggregate roots in one transaction as the event store of type '{0}' commits one stream at a time.
To fix this issue, either:
- implement 'dddlib.Persistence.Sdk.IMultiStreamEventStore' on the event store, or
- save the aggregate roots one at a time.",
                    this.eventStore.GetType()));
        }

        try
        {
            await this.SaveManyInternalAsync(aggregateRoots, multiStreamEventStore, correlationId, cancellationToken).ConfigureAwait(false);
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
            throw NoEventsToSave(aggregateRoot);
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

    private static PersistenceException NoEventsToSave(AggregateRoot aggregateRoot) =>
        new(
            string.Format(
                CultureInfo.InvariantCulture,
                @"Cannot save initial commit for aggregate root of type '{0}' as there are no events to save.
To fix this issue:
- ensure that your aggregate root is configured to use event application, and
- ensure that the events are getting applied using the base 'Apply' method.
Further information: https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-event-application.md",
                aggregateRoot.GetType()));

    private static ConcurrencyException Conflict(PendingSave save, string reason, Exception? innerException = null) =>
        new(
            string.Format(
                CultureInfo.InvariantCulture,
                "The aggregate root of type '{0}' with natural key '{1}' {2}.",
                save.RuntimeType.RuntimeType,
                save.NaturalKey,
                reason),
            innerException);

    private async Task SaveManyInternalAsync(IReadOnlyCollection<AggregateRoot> aggregateRoots, IMultiStreamEventStore multiStreamEventStore, Guid correlationId, CancellationToken cancellationToken)
    {
        // Every aggregate root is validated before anything is written, the identity map included.
        var saves = new List<PendingSave>(aggregateRoots.Count);
        foreach (var aggregateRoot in aggregateRoots)
        {
            var runtimeType = Application.Current.GetAggregateRootType(aggregateRoot.GetType());
            runtimeType.ValidateForPersistence();

            var naturalKey = runtimeType.GetNaturalKey(aggregateRoot);
            var events = aggregateRoot.GetUncommittedEvents().ToArray();
            if (aggregateRoot.State is null && events.Length == 0)
            {
                throw NoEventsToSave(aggregateRoot);
            }

            if (events.Length > 0)
            {
                saves.Add(new PendingSave(aggregateRoot, runtimeType, naturalKey, events));
            }
        }

        // Identities are resolved outside the commit, as for a single save: a failed commit can leave a new aggregate
        // root's identity registered, and the next save of that natural key reuses it.
        var commits = new List<StreamCommit>(saves.Count);
        var savesByStream = new Dictionary<Guid, PendingSave>(saves.Count);
        foreach (var save in saves)
        {
            var naturalKeyType = save.RuntimeType.NaturalKey!.PropertyType;
            var preCommitState = save.AggregateRoot.State;
            var streamId = preCommitState is null
                ? await this.identityMap.GetOrAddAsync(save.RuntimeType.RuntimeType, naturalKeyType, save.NaturalKey, cancellationToken).ConfigureAwait(false)
                : await this.identityMap.TryGetAsync(save.RuntimeType.RuntimeType, naturalKeyType, save.NaturalKey, cancellationToken).ConfigureAwait(false)
                    ?? throw Conflict(save, "does not exist");

            if (!savesByStream.TryAdd(streamId, save))
            {
                throw Conflict(save, "appears more than once in the aggregate roots");
            }

            commits.Add(new StreamCommit(streamId, save.Events, preCommitState));
        }

        if (commits.Count == 0)
        {
            return;
        }

        IReadOnlyList<string> postCommitStates;
        try
        {
            postCommitStates = await multiStreamEventStore.CommitStreamsAsync(commits, correlationId, cancellationToken).ConfigureAwait(false);
        }
        catch (StreamConcurrencyException ex) when (savesByStream.TryGetValue(ex.StreamId, out var failed))
        {
            throw Conflict(failed, failed.AggregateRoot.State is null ? "already exists" : "has changed since it was loaded", ex);
        }

        // Only now, with every stream committed, do the aggregate roots take their new state.
        for (var index = 0; index < commits.Count; index++)
        {
            savesByStream[commits[index].StreamId].AggregateRoot.CommitEvents(postCommitStates[index]);
        }

        foreach (var commit in commits)
        {
            if (savesByStream[commit.StreamId].AggregateRoot.IsDestroyed)
            {
                await this.identityMap.RemoveAsync(commit.StreamId, cancellationToken).ConfigureAwait(false);
            }
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

        var aggregateRoot = AggregateRootFactory.Create<T>(snapshot.Memento, snapshot.StreamRevision, stream.Events, stream.State);
        if (aggregateRoot.IsDestroyed)
        {
            await this.identityMap.RemoveAsync(streamId.Value, cancellationToken).ConfigureAwait(false);
            runtimeType.ThrowNotFound(naturalKey);
        }

        return aggregateRoot;
    }

    private sealed record PendingSave(AggregateRoot AggregateRoot, AggregateRootType RuntimeType, object NaturalKey, object[] Events);
}
