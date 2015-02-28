// <copyright file="EventStoreRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    using dddlib.Persistence.Sdk;

    /// <summary>
    /// Represents the aggregate root repository.
    /// </summary>
    public class EventStoreRepository : RepositoryBase, IEventStoreRepository
    {
        private readonly IEventStore eventStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventStoreRepository" /> class.
        /// </summary>
        /// <param name="identityMap">The identity map.</param>
        /// <param name="eventStore">The event store.</param>
        public EventStoreRepository(IIdentityMap identityMap, IEventStore eventStore)
            : base(identityMap)
        {
            Guard.Against.Null(() => eventStore);

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

            var id = this.GetId(aggregateRoot);

            ////var memento = aggregateRoot.GetMemento();
            var events = aggregateRoot.GetUncommittedEvents();

            var state = aggregateRoot.State;
            if (state == null)
            {
                // NOTE (Cameron): This is the initial commit, for what it's worth.
            }

            // TODO (Cameron): Try catch around commit stream.
            var newState = default(string);
            this.eventStore.CommitStream(id, events, state, out newState);

            // TODO (Cameron): Save the memento with the new commits if the state is the same as the old state and replace the state with the new state.
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
            var id = this.GetId<T>(naturalKey);

            var state = default(string);
            var events = this.eventStore.GetStream(id, out state);

            return this.Reconstitute<T>(null, events, state);
        }
    }
}
