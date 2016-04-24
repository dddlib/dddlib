// <copyright file="DefaultTypeAnalyzerService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Services.TypeAnalyzer
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using dddlib.Runtime;
    using dddlib.Sdk.Configuration.Model;

    /// <summary>
    /// Represents the default type analyzer service.
    /// </summary>
    public class DefaultTypeAnalyzerService : ITypeAnalyzerService
    {
        /// <summary>
        /// Determines whether the specified runtime type is a valid aggregate root.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <returns>Returns <c>true</c> when the runtime type is an aggregate root; otherwise <c>false</c>.</returns>
        public bool IsValidAggregateRoot(Type runtimeType)
        {
            return typeof(AggregateRoot).IsAssignableFrom(runtimeType);
        }

        /// <summary>
        /// Determines whether the specified runtime type is a valid entity.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <returns>Returns <c>true</c> when the runtime type is an entity; otherwise <c>false</c>.</returns>
        public bool IsValidEntity(Type runtimeType)
        {
            return typeof(Entity).IsAssignableFrom(runtimeType);
        }

        /// <summary>
        /// Determines whether the specified runtime type is a valid value object.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <returns>Returns <c>true</c> when the runtime type is a value object; otherwise <c>false</c>.</returns>
        public bool IsValidValueObject(Type runtimeType)
        {
            return runtimeType.IsSubclassOfRawGeneric(typeof(ValueObject<>));
        }

        /// <summary>
        /// Determines whether the specified runtime type contains a specific property.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyType">The property type.</param>
        /// <returns>Returns <c>true</c> when the runtime type contains the property; otherwise <c>false</c>.</returns>
        public bool IsValidProperty(Type runtimeType, string propertyName, Type propertyType)
        {
            var property = runtimeType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(member => member.Name.Equals(propertyName))
                .Where(member => member.PropertyType == propertyType)
                .SingleOrDefault();

            return property != null;
        }

        /// <summary>
        /// Gets the natural key for the specified runtime type.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <returns>The natural key.</returns>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        public NaturalKey GetNaturalKey(Type runtimeType)
        {
            Guard.Against.Null(() => runtimeType);

            var naturalKeys = runtimeType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(member => member.GetCustomAttributes(typeof(dddlib.NaturalKey), false).SingleOrDefault() != null)
                .ToArray();

            if (naturalKeys.Length > 1)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"Entity of type '{0}' has more than one natural key defined.
To fix this issue:
- ensure that there is only a single natural key defined for the entity.",
                        runtimeType))
                {
                    HelpLink = "https://github.com/dddlib/dddlib/wiki/Entity-Equality",
                };
            }

            if (naturalKeys.Length == 0)
            {
                return null;
            }

            return new NaturalKey(naturalKeys[0].DeclaringType, naturalKeys[0].Name, naturalKeys[0].PropertyType, this);
        }

        /// <summary>
        /// Gets the uninitialized factory for the specified runtime type.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <returns>The uninitialized factory.</returns>
        public Delegate GetUninitializedFactory(Type runtimeType)
        {
            Guard.Against.Null(() => runtimeType);

            if (runtimeType.IsAbstract)
            {
                return null;
            }

            var defaultConstructor = runtimeType.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);

            if (defaultConstructor == null)
            {
                return null;
            }

            var body = Expression.New(defaultConstructor);
            var type = typeof(Func<>).MakeGenericType(runtimeType);
            var lambda = Expression.Lambda(type, body);

            return lambda.Compile();
        }
    }
}
