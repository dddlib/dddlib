// <copyright file="AggregateRootRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents the aggregate root repository.
    /// </summary>
    public class AggregateRootRepository
    {
        /// <summary>
        /// Saves the specified aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="aggregateRoot">The aggregate root.</param>
        public void Save<T>(T aggregateRoot) where T : AggregateRoot
        {
            var memento = aggregateRoot.GetMemento();
            var events = aggregateRoot.GetUncommittedEvents();
            var state = aggregateRoot.State;

            // save?
            var newState = default(string);

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
