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
        // NOTE (Cameron): The aggregate root factory used to be part of this class but I've split out for reuse. Not sure it's worth injecting.
        private readonly AggregateRootFactory factory = new AggregateRootFactory();
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

            if (aggregateRootType.UninitializedFactory == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cannot save aggregate root of type '{0}' as there is no uninitialized factory defined for reconstitution.", 
                        type));
            }

            var naturalKey = aggregateRootType.NaturalKey.GetValue(aggregateRoot);
            if (naturalKey == null)
            {
                // NOTE (Cameron): This mimics the Guard clause functionality by design.
                throw new ArgumentException(
                    "Value cannot be null.",
                    string.Concat(Guard.Expression.Parse(() => aggregateRoot), ".", aggregateRootType.NaturalKey.PropertyName));
            }

            return this.identityMap.GetOrAdd(aggregateRootType.RuntimeType, aggregateRootType.NaturalKey.PropertyType, naturalKey);
        }

        /// <summary>
        /// Gets the identifier for the aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The identifier for the aggregate root.</returns>
        protected Guid GetId<T>(object naturalKey) where T : AggregateRoot
        {
            Guard.Against.Null(() => naturalKey);

            var aggregateRootType = Application.Current.GetAggregateRootType(typeof(T));
            if (aggregateRootType.NaturalKey == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cannot load aggregate root of type '{0}' as there is no natural key defined.",
                        typeof(T)));
            }

            if (aggregateRootType.NaturalKey.PropertyType != naturalKey.GetType())
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid value for aggregate root of type '{0}'. That value type must match natural key type of '{1}' but is type of '{2}'.",
                        typeof(T),
                        aggregateRootType.NaturalKey.PropertyType,
                        naturalKey.GetType()),
                    Guard.Expression.Parse(() => naturalKey));
            }

            var identity = default(Guid);
            if (!this.identityMap.TryGet(aggregateRootType.RuntimeType, aggregateRootType.NaturalKey.PropertyType, naturalKey, out identity))
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
        /// <param name="memento">The memento.</param>
        /// <param name="revision">The revision.</param>
        /// <param name="events">The events that contain the state of the aggregate root.</param>
        /// <param name="state">A checksum that correlates to the state of the aggregate root in the persistence layer for the given events.</param>
        /// <returns>The specified aggregate root.</returns>
        protected T Reconstitute<T>(object memento, int revision, IEnumerable<object> events, string state)
            where T : AggregateRoot
        {
            return this.factory.Create<T>(memento, revision, events, state);
        }
    }
}
