// <copyright file="DefaultTypeAnalyzerService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Services.TypeAnalyzer
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using dddlib.Runtime;
    using dddlib.Sdk.Configuration.Model;

    internal class DefaultTypeAnalyzerService : ITypeAnalyzerService
    {
        public bool IsValidAggregateRoot(Type runtimeType)
        {
            return typeof(AggregateRoot).IsAssignableFrom(runtimeType);
        }

        public bool IsValidEntity(Type runtimeType)
        {
            return typeof(Entity).IsAssignableFrom(runtimeType);
        }

        public bool IsValidValueObject(Type runtimeType)
        {
            return runtimeType.IsSubclassOfRawGeneric(typeof(ValueObject<>));
        }

        public bool IsValidProperty(Type runtimeType, string propertyName, Type propertyType)
        {
            var property = runtimeType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(member => member.Name.Equals(propertyName))
                .Where(member => member.PropertyType == propertyType)
                .SingleOrDefault();

            return property != null;
        }

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
                        "Entity of type '{0}' has more than one natural key defined.",
                        runtimeType));
            }

            if (naturalKeys.Length == 0)
            {
                return null;
            }

            return new NaturalKey(naturalKeys[0].DeclaringType, naturalKeys[0].Name, naturalKeys[0].PropertyType, this);
        }

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
