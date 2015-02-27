// <copyright file="Repository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using dddlib.Runtime;

    /// <summary>
    /// Represents the aggregate root repository.
    /// </summary>
    /// <typeparam name="T">The type of aggregate root.</typeparam>
    public abstract class Repository<T> : IRepository<T> where T : AggregateRoot
    {
        private readonly IIdentityMap identityMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="Repository{T}"/> class.
        /// </summary>
        /// <param name="identityMap">The identity map.</param>
        public Repository(IIdentityMap identityMap)
        {
            Guard.Against.Null(() => identityMap);

            this.identityMap = identityMap;
        }

        /// <summary>
        /// Saves the specified aggregate root.
        /// </summary>
        /// <param name="aggregateRoot">The aggregate root.</param>
        public void Save(T aggregateRoot)
        {
            Guard.Against.Null(() => aggregateRoot);

            var type = aggregateRoot.GetType(); // NOTE (Cameron): Because we can't trust typeof(T) as it may be the base class.

            var aggregateRootType = Application.Current.GetAggregateRootType(type);
            if (aggregateRootType.NaturalKey == null)
            {
                // TODO (Cameron): Exception text.
                throw new RuntimeException("Cannot save an aggregate root without a defined natural key.");
            }

            var naturalKey = aggregateRootType.NaturalKey.GetValue(aggregateRoot);
            var id = this.identityMap.GetOrAdd(type, naturalKey, aggregateRootType.NaturalKeyEqualityComparer);

            var memento = aggregateRoot.GetMemento();
            if (memento == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The aggregate root of type '{0}' has not been configured to create a memento representing its state.",
                        type));
            }

            var state = aggregateRoot.State;
            if (state == null)
            {
                // NOTE (Cameron): This is the initial commit, for what it's worth.
            }

            // NOTE (Cameron): Save.
            string newState;
            this.Save(id, memento, state, out newState);

            aggregateRoot.CommitEvents(newState);
        }

        /// <summary>
        /// Loads the aggregate root with the specified natural key.
        /// </summary>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The aggregate root.</returns>
        public T Load(object naturalKey)
        {
            var id = this.identityMap.Get(typeof(T), naturalKey);

            var state = default(string);
            var memento = this.Load(id, out state);
            
            var factory = new AggregateRootFactory();
            var aggregateRoot = factory.Reconstitute<T>(memento, Enumerable.Empty<object>(), state);
            return aggregateRoot;
        }

        /// <summary>
        /// Saves the memento for the specified identifier providing the state is valid.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="memento">The memento.</param>
        /// <param name="oldState">The old state.</param>
        /// <param name="newState">The new state.</param>
        protected abstract void Save(Guid id, object memento, string oldState, out string newState);

        /// <summary>
        /// Loads the memento and the state for the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="state">The state.</param>
        /// <returns>The memento.</returns>
        protected abstract object Load(Guid id, out string state);
    }
}
