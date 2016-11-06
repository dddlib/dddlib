// <copyright file="AggregateRootTypeExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using dddlib.Sdk.Configuration.Model;
    using Runtime;

    /// <summary>
    /// Contains extension methods for <see cref="dddlib.Sdk.Configuration.Model.AggregateRootType"/>.
    /// </summary>
    public static class AggregateRootTypeExtensions
    {
        /// <summary>
        /// Ensures that the aggregate root type is valid for persistence.
        /// </summary>
        /// <param name="aggregateRootType">The aggregate root type.</param>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public static void ValidateForPersistence(this AggregateRootType aggregateRootType)
        {
            if (aggregateRootType.NaturalKey == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"The aggregate root of type '{0}' does not have a natural key defined.
To fix this issue, either:
- use a bootstrapper to define a natural key, or
- decorate the natural key property on the aggregate root with the [dddlib.NaturalKey] attribute.",
                        aggregateRootType.RuntimeType))
                {
                    HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Equality",
                };
            }

            if (aggregateRootType.UninitializedFactory == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"The aggregate root of type '{0}' does not have a factory method registered with the runtime.
To fix this issue, either:
- use a bootstrapper to register a factory method with the runtime, or
- add a (protected internal) default constructor to the aggregate root.",
                        aggregateRootType.RuntimeType))
                {
                    HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Reconstitution",
                };
            }
        }

        /// <summary>
        /// Ensures that the specified natural key is valid.
        /// </summary>
        /// <param name="aggregateRootType">The aggregate root type.</param>
        /// <param name="naturalKey">The natural key.</param>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public static void Validate(this AggregateRootType aggregateRootType, object naturalKey)
        {
            if (aggregateRootType.NaturalKey.PropertyType != naturalKey.GetType())
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid natural key value type for aggregate root of type '{0}'. Expected value type of '{1}' but value is type of '{2}'.",
                        aggregateRootType.RuntimeType,
                        aggregateRootType.NaturalKey.PropertyType,
                        naturalKey.GetType()),
                    Guard.Expression.Parse(() => naturalKey));
            }
        }

        /// <summary>
        /// Throws an <see cref="dddlib.Persistence.AggregateRootNotFoundException"/> for the specified natural key.
        /// </summary>
        /// <param name="aggregateRootType">The aggregate root type.</param>
        /// <param name="naturalKey">The natural key.</param>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public static void ThrowNotFound(this AggregateRootType aggregateRootType, object naturalKey)
        {
            throw new AggregateRootNotFoundException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Cannot find the aggregate root of type '{0}' with natural key '{1}'.",
                    aggregateRootType.RuntimeType,
                    naturalKey));
        }

        /// <summary>
        /// Gets the natural key for the specified aggregate root.
        /// </summary>
        /// <param name="aggregateRootType">The aggregate root type.</param>
        /// <param name="aggregateRoot">The aggregate root.</param>
        /// <returns>The natural key.</returns>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public static object GetNaturalKey(this AggregateRootType aggregateRootType, AggregateRoot aggregateRoot)
        {
            var naturalKey = aggregateRootType.NaturalKey.GetValue(aggregateRoot);
            if (naturalKey == null)
            {
                // NOTE (Cameron): This mimics the Guard clause functionality by design.
                throw new ArgumentException(
                    "Value cannot be null.",
                    string.Concat(Guard.Expression.Parse(() => aggregateRoot), ".", aggregateRootType.NaturalKey.PropertyName));
            }

            return naturalKey;
        }
    }
}
