// <copyright file="RepositoryBase.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using dddlib.Runtime;
    using dddlib.Sdk.Configuration.Model;

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
            var runtimeType = Application.Current.GetAggregateRootType(type);

            Validate(runtimeType, type);

            var naturalKey = runtimeType.NaturalKey.GetValue(aggregateRoot);
            if (naturalKey == null)
            {
                // NOTE (Cameron): This mimics the Guard clause functionality by design.
                throw new ArgumentException(
                    "Value cannot be null.",
                    string.Concat(Guard.Expression.Parse(() => aggregateRoot), ".", runtimeType.NaturalKey.PropertyName));
            }

            return this.identityMap.GetOrAdd(runtimeType.RuntimeType, runtimeType.NaturalKey.PropertyType, naturalKey);
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

            var runtimeType = Application.Current.GetAggregateRootType(typeof(T));

            Validate(runtimeType, typeof(T));

            if (runtimeType.NaturalKey.PropertyType != naturalKey.GetType())
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid natural key value type for aggregate root of type '{0}'. Expected value type of '{1}' but value is type of '{2}'.",
                        typeof(T),
                        runtimeType.NaturalKey.PropertyType,
                        naturalKey.GetType()),
                    Guard.Expression.Parse(() => naturalKey));
            }

            var identity = default(Guid);
            if (!this.identityMap.TryGet(runtimeType.RuntimeType, runtimeType.NaturalKey.PropertyType, naturalKey, out identity))
            {
                throw new AggregateRootNotFoundException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cannot find the aggregate root of type '{0}' with natural key '{1}'.",
                        typeof(T),
                        naturalKey));
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

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        private static void Validate(AggregateRootType runtimeType, Type type)
        {
            if (runtimeType.NaturalKey == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"The aggregate root of type '{0}' does not have a natural key defined.
To fix this issue, either:
- use a bootstrapper to define a natural key, or
- decorate the natural key property on the aggregate root with the [dddlib.NaturalKey] attribute.",
                        type))
                {
                    HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Equality",
                };
            }

            // NOTE (Cameron): Exact duplication of the code in AggregateRootFactory...
            if (runtimeType.UninitializedFactory == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"The aggregate root of type '{0}' does not have a factory method registered with the runtime.
To fix this issue, either:
- use a bootstrapper to register a factory method with the runtime, or
- add a default constructor to the aggregate root.",
                        type))
                {
                    HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Reconstitution",
                };
            }
        }
    }
}
