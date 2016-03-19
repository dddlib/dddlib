// <copyright file="Repository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;
    using System.Globalization;
    using System.Linq;
    using Runtime;

    /// <summary>
    /// Represents an aggregate root repository.
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

            try
            {
                this.SaveInternal(aggregateRoot);
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
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The aggregate root.</returns>
        public T Load(object naturalKey)
        {
            Guard.Against.Null(() => naturalKey);

            try
            {
                return this.LoadInternal(naturalKey);
            }
            catch (RuntimeException ex)
            {
                throw new PersistenceException(
                    string.Concat("An exception occurred during the load operation.\r\n", ex.Message),
                    ex);
            }
        }

        /// <summary>
        /// Saves the memento for the specified identifier providing the state is valid.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="memento">The memento.</param>
        /// <param name="preCommitState">The pre-commit state of the memento.</param>
        /// <param name="postCommitState">The post-commit state of memento.</param>
        protected abstract void Save(Guid id, object memento, string preCommitState, out string postCommitState);

        /// <summary>
        /// Loads the memento and the state for the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="state">The state.</param>
        /// <returns>The memento.</returns>
        protected abstract object Load(Guid id, out string state);

        private void SaveInternal(T aggregateRoot)
        {
            var id = this.GetId(aggregateRoot);

            var memento = aggregateRoot.GetMemento();
            if (memento == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The aggregate root of type '{0}' has not been configured to return a memento representing its state.",
                        this.GetType()))
                {
                    HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Mementos",
                };
            }

            var preCommitState = aggregateRoot.State;
            if (preCommitState == null)
            {
                // NOTE (Cameron): This is the initial commit, for what it's worth.
            }

            // TODO (Cameron): Try catch around save.
            var postCommitState = default(string);
            this.Save(id, memento, preCommitState, out postCommitState);

            aggregateRoot.CommitEvents(postCommitState);
        }

        private T LoadInternal(object naturalKey)
        {
            var id = this.GetId<T>(naturalKey);

            var state = default(string);
            var memento = this.Load(id, out state);

            return this.Reconstitute<T>(memento, 0, Enumerable.Empty<object>(), state);
        }
    }
}
