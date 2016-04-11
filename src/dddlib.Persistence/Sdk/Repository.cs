// <copyright file="Repository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using Runtime;

    /// <summary>
    /// Represents an aggregate root repository.
    /// </summary>
    /// <typeparam name="T">The type of aggregate root.</typeparam>
    public abstract class Repository<T> : IRepository<T> where T : AggregateRoot
    {
        // NOTE (Cameron): The aggregate root factory used to be part of this class but I've split out for reuse. Not sure it's worth injecting.
        private readonly AggregateRootFactory factory = new AggregateRootFactory();
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

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        private void SaveInternal(T aggregateRoot)
        {
            // NOTE (Cameron): Because we can't trust type of(T) as it may be the base class.
            var type = aggregateRoot.GetType();
            var runtimeType = Application.Current.GetAggregateRootType(type);

            runtimeType.ValidateForPersistence();

            var naturalKey = runtimeType.GetNaturalKey(aggregateRoot);
            var id = this.identityMap.GetOrAdd(runtimeType.RuntimeType, runtimeType.NaturalKey.PropertyType, naturalKey);

            var preCommitState = aggregateRoot.State;
            if (preCommitState == null)
            {
                // NOTE (Cameron): This is the initial commit, for what it's worth.
            }

            var memento = aggregateRoot.GetMemento();
            if (memento == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"The aggregate root of type '{0}' has not been configured to return a memento representing its state.
To fix this issue:
- override the 'GetState' method of the aggregate root to return a memento describing it's state.",
                        type))
                {
                    HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Mementos",
                };
            }

            var postCommitState = default(string);
            this.Save(id, memento, preCommitState, out postCommitState);

            aggregateRoot.CommitEvents(postCommitState);

            if (aggregateRoot.IsDestroyed)
            {
                this.identityMap.Remove(id);
            }
        }

        private T LoadInternal(object naturalKey)
        {
            var runtimeType = Application.Current.GetAggregateRootType(typeof(T));

            runtimeType.ValidateForPersistence();
            runtimeType.Validate(naturalKey);

            var id = default(Guid);
            if (!this.identityMap.TryGet(runtimeType.RuntimeType, runtimeType.NaturalKey.PropertyType, naturalKey, out id))
            {
                runtimeType.ThrowNotFound(naturalKey);
            }

            var state = default(string);
            var memento = this.Load(id, out state);

            var aggregateRoot = this.factory.Create<T>(memento, 0, Enumerable.Empty<object>(), state);
            if (aggregateRoot.IsDestroyed)
            {
                // NOTE (Cameron): We've hit an odd situation where we've got an aggregate whose lifecycle has ended.
                this.identityMap.Remove(id);
            }

            if (aggregateRoot == null || aggregateRoot.IsDestroyed)
            {
                runtimeType.ThrowNotFound(naturalKey);
            }

            return aggregateRoot;
        }
    }
}
