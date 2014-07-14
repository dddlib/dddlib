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

        /// <summary>
        /// Initializes a new instance of the <see cref="EventStoreRepository"/> class.
        /// </summary>
        /// <param name="identityMap">The identity map.</param>
        public EventStoreRepository(IIdentityMap identityMap)
        {
            Guard.Against.Null(() => identityMap);

            this.identityMap = identityMap;
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

            var naturalKey = entityType.NaturalKeySelector.Invoke(aggregateRoot);
            var streamID = this.identityMap.Map(type, naturalKey);

            var memento = aggregateRoot.GetMemento();
            var events = aggregateRoot.GetUncommittedEvents();

            var state = aggregateRoot.State;
            if (state == null)
            {
                // NOTE (Cameron): This is the initial commit, for what it's worth.
            }

            var newState = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

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
            var memento = default(object); // get snapshot
            var events = default(IEnumerable<object>); // get events
            var state = default(string); // get state

            var factory = new AggregateRootFactory();
            var aggregateRoot = factory.Reconstitute<T>(memento, events, state);
            return aggregateRoot;
        }
    }
}
