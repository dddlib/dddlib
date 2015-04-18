// <copyright file="RepositoryBase.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using dddlib.Runtime;

    /// <summary>
    /// Represents the aggregate root repository.
    /// </summary>
    public abstract class RepositoryBase
    {
        private readonly IIdentityMap identityMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryBase" /> class.
        /// </summary>
        /// <param name="identityMap">The identity map.</param>
        public RepositoryBase(IIdentityMap identityMap)
        {
            Guard.Against.Null(() => identityMap);

            this.identityMap = identityMap;
        }

        /// <summary>
        /// Gets the identifier for the aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="aggregateRoot">The aggregate root.</param>
        /// <returns>The identifier for the aggregate root.</returns>
        protected Guid GetId<T>(T aggregateRoot) where T : AggregateRoot
        {
            Guard.Against.Null(() => aggregateRoot);

            // NOTE (Cameron): Because we can't trust type of(T) as it may be the base class.
            var type = aggregateRoot.GetType();

            var aggregateRootType = Application.Current.GetAggregateRootType(type);
            if (aggregateRootType.NaturalKey == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture, 
                        "Cannot save aggregate root of type '{0}' as there is no natural key defined.", 
                        type));
            }

            var naturalKey = aggregateRootType.NaturalKey.GetValue(aggregateRoot);
            return this.identityMap.GetOrAdd(type, naturalKey);
        }

        /// <summary>
        /// Gets the identifier for the aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The identifier for the aggregate root.</returns>
        protected Guid GetId<T>(object naturalKey) where T : AggregateRoot
        {
            var identity = default(Guid);
            if (!this.identityMap.TryGet(typeof(T), naturalKey, out identity))
            {
                // aggregate root not found?
                throw new AggregateRootNotFoundException();
            }

            return identity;
        }

        /// <summary>
        /// Reconstitutes the specified aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="memento">The memento, or null.</param>
        /// <param name="events">The events that contain the state of the aggregate root.</param>
        /// <param name="state">A checksum that correlates to the state of the aggregate root in the persistence layer for the given events.</param>
        /// <returns>The specified aggregate root.</returns>
        protected T Reconstitute<T>(object memento, IEnumerable<object> events, string state)
            where T : AggregateRoot
        {
            // TODO (Cameron): Make this more performant. Consider using some type of IL instantiation.
            var runtimeType = Application.Current.GetAggregateRootType(typeof(T));
            if (runtimeType.UninitializedFactory == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The aggregate root of type '{0}' does not have a factory method registered with the runtime.",
                        typeof(T)));
            }

            var uninitializedFactory = (Func<T>)runtimeType.UninitializedFactory;
            var aggregateRoot = uninitializedFactory.Invoke();
            aggregateRoot.Initialize(memento, events, state);
            return aggregateRoot;
        }
    }
}
