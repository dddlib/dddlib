// <copyright file="EventStoreRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using dddlib.Runtime;

    /// <summary>
    /// Represents an event store repository.
    /// </summary>
    public class EventStoreRepository : IEventStoreRepository
    {
        // NOTE (Cameron): The aggregate root factory used to be part of this class but I've split out for reuse. Not sure it's worth injecting.
        private readonly AggregateRootFactory factory = new AggregateRootFactory();
        private readonly IIdentityMap identityMap;
        private readonly IEventStore eventStore;
        private readonly ISnapshotStore snapshotStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventStoreRepository" /> class.
        /// </summary>
        /// <param name="identityMap">The identity map.</param>
        /// <param name="eventStore">The event store.</param>
        /// <param name="snapshotStore">The snapshot store.</param>
        public EventStoreRepository(IIdentityMap identityMap, IEventStore eventStore, ISnapshotStore snapshotStore)
        {
            Guard.Against.Null(() => identityMap);
            Guard.Against.Null(() => eventStore);
            Guard.Against.Null(() => snapshotStore);

            this.identityMap = identityMap;
            this.eventStore = eventStore;
            this.snapshotStore = snapshotStore;
        }

        /// <summary>
        /// Saves the specified aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="aggregateRoot">The aggregate root.</param>
        public void Save<T>(T aggregateRoot) where T : AggregateRoot
        {
            this.Save(aggregateRoot, Guid.NewGuid());
        }

        /// <summary>
        /// Saves the specified aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="aggregateRoot">The aggregate root.</param>
        /// <param name="correlationId">The correlation identifier.</param>
        public void Save<T>(T aggregateRoot, Guid correlationId) where T : AggregateRoot
        {
            Guard.Against.Null(() => aggregateRoot);

            try
            {
                this.SaveInternal(aggregateRoot, correlationId);
            }
            catch (RuntimeException ex)
            {
                throw new PersistenceException(
                    string.Concat("An exception occurred during the save operation.\r\n", ex.Message),
                    ex);
            }
        }

        /// <summary>
        /// Loads the aggregate root with the specified natural key.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The aggregate root.</returns>
        public T Load<T>(object naturalKey) where T : AggregateRoot
        {
            Guard.Against.Null(() => naturalKey);

            try
            {
                return this.LoadInternal<T>(naturalKey);
            }
            catch (RuntimeException ex)
            {
                throw new PersistenceException(
                    string.Concat("An exception occurred during the load operation.\r\n", ex.Message),
                    ex);
            }
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        private void SaveInternal<T>(T aggregateRoot, Guid correlationId) where T : AggregateRoot
        {
            // NOTE (Cameron): Because we can't trust type of(T) as it may be the base class.
            var type = aggregateRoot.GetType();
            var runtimeType = Application.Current.GetAggregateRootType(type);

            runtimeType.ValidateForPersistence();

            var naturalKey = runtimeType.GetNaturalKey(aggregateRoot);
            var streamId = this.identityMap.GetOrAdd(runtimeType.RuntimeType, runtimeType.NaturalKey.PropertyType, naturalKey);
            var events = aggregateRoot.GetUncommittedEvents();

            var preCommitState = aggregateRoot.State;
            if (preCommitState == null && !events.Any())
            {
                // NOTE (Cameron): This is the initial commit so there should be events. It's odd but if we don't throw we may confuse people.
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

            if (!events.Any())
            {
                // TODO (Cameron): Nothing to commit. Log info?
                return;
            }

            var postCommitState = default(string);
            this.eventStore.CommitStream(streamId, events, correlationId, preCommitState, out postCommitState);

            aggregateRoot.CommitEvents(postCommitState);

            if (aggregateRoot.IsDestroyed)
            {
                this.identityMap.Remove(streamId);
            }
        }

        private T LoadInternal<T>(object naturalKey) where T : AggregateRoot
        {
            var runtimeType = Application.Current.GetAggregateRootType(typeof(T));

            runtimeType.ValidateForPersistence();
            runtimeType.Validate(naturalKey);

            var streamId = default(Guid);
            if (!this.identityMap.TryGet(runtimeType.RuntimeType, runtimeType.NaturalKey.PropertyType, naturalKey, out streamId))
            {
                runtimeType.ThrowNotFound(naturalKey);
            }

            var state = default(string);
            var snapshot = this.snapshotStore.GetSnapshot(streamId) ?? new Snapshot();
            var events = this.eventStore.GetStream(streamId, snapshot.StreamRevision, out state);
            if (snapshot.StreamRevision == 0 && !events.Any())
            {
                runtimeType.ThrowNotFound(naturalKey);
            }

            var aggregateRoot = this.factory.Create<T>(snapshot.Memento, snapshot.StreamRevision, events, state);
            if (aggregateRoot.IsDestroyed)
            {
                // NOTE (Cameron): We've hit an odd situation where we've got an aggregate whose lifecycle has ended.
                this.identityMap.Remove(streamId);
            }

            if (aggregateRoot == null || aggregateRoot.IsDestroyed)
            {
                runtimeType.ThrowNotFound(naturalKey);
            }

            return aggregateRoot;
        }
    }
}
