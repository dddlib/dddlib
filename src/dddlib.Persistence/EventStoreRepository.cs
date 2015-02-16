// <copyright file="EventStoreRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Represents the aggregate root repository.
    /// </summary>
    public class EventStoreRepository : IEventStoreRepository
    {
        private readonly IIdentityMap identityMap;
        private readonly IEventStore eventStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventStoreRepository" /> class.
        /// </summary>
        /// <param name="identityMap">The identity map.</param>
        /// <param name="eventStore">The event store.</param>
        public EventStoreRepository(IIdentityMap identityMap, IEventStore eventStore)
        {
            Guard.Against.Null(() => identityMap);
            Guard.Against.Null(() => eventStore);

            this.identityMap = identityMap;
            this.eventStore = eventStore;
        }

        /// <summary>
        /// Saves the specified aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="aggregateRoot">The aggregate root.</param>
        public void Save<T>(T aggregateRoot) where T : AggregateRoot
        {
            Guard.Against.Null(() => aggregateRoot);

            var type = aggregateRoot.GetType(); // NOTE (Cameron): Because we can't trust typeof(T) as it may be the base class.

            var aggregateRootType = dddlib.Runtime.Application.Current.GetAggregateRootType(type);
            var entityType = dddlib.Runtime.Application.Current.GetEntityType(type);

            var naturalKey = entityType.NaturalKey.GetValue(aggregateRoot);
            var id = this.identityMap.GetOrAdd(type, naturalKey, entityType.NaturalKeyEqualityComparer);

            ////var memento = aggregateRoot.GetMemento();
            var events = aggregateRoot.GetUncommittedEvents();

            var state = aggregateRoot.State;
            if (state == null)
            {
                // NOTE (Cameron): This is the initial commit, for what it's worth.
            }

            var newState = default(string);
            this.eventStore.CommitStream(id, events, state, out newState);

            // NOTE (Cameron): Save.
            // save the memento with the new commits if the state is the same as the old state and replace the state with the new state.
            aggregateRoot.CommitEvents(newState);
        }

        /// <summary>
        /// Loads the aggregate root with the specified natural key.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The aggregate root.</returns>
        public T Load<T>(object naturalKey) where T : AggregateRoot
        {
            var id = this.identityMap.Get(typeof(T), naturalKey);

            var state = default(string); // get state
            var events = this.eventStore.GetStream(id, out state);

            var factory = new AggregateRootFactory();
            var aggregateRoot = factory.Reconstitute<T>(null, events, state);
            return aggregateRoot;
        }
    }
}
