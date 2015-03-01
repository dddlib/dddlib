// <copyright file="Repository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    using System;
    using System.Globalization;
    using System.Linq;
    using dddlib.Persistence.Sdk;
    using dddlib.Runtime;

    /// <summary>
    /// Represents the aggregate root repository.
    /// </summary>
    /// <typeparam name="T">The type of aggregate root.</typeparam>
    public abstract class Repository<T> : RepositoryBase, IRepository<T> where T : AggregateRoot
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Repository{T}"/> class.
        /// </summary>
        /// <param name="identityMap">The identity map.</param>
        public Repository(IIdentityMap identityMap)
            : base(identityMap)
        {
        }

        /// <summary>
        /// Saves the specified aggregate root.
        /// </summary>
        /// <param name="aggregateRoot">The aggregate root.</param>
        public void Save(T aggregateRoot)
        {
            Guard.Against.Null(() => aggregateRoot);

            var id = this.GetId(aggregateRoot);

            var memento = aggregateRoot.GetMemento();
            if (memento == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cannot save aggregate root of type '{0}' as there is no configured memento representing its state.",
                        aggregateRoot.GetType()));
            }

            var state = aggregateRoot.State;
            if (state == null)
            {
                // NOTE (Cameron): This is the initial commit, for what it's worth.
            }

            // TODO (Cameron): Try catch around save.
            var newState = default(string);
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
            var id = this.GetId<T>(naturalKey);

            var state = default(string);
            var memento = this.Load(id, out state);
            
            return this.Reconstitute<T>(memento, Enumerable.Empty<object>(), state);
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
