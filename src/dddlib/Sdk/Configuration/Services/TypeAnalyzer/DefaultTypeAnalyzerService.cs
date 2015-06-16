// <copyright file="DefaultTypeAnalyzerService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Services.TypeAnalyzer
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using dddlib.Runtime;
    using dddlib.Sdk.Configuration.Model;

    internal class DefaultTypeAnalyzerService : ITypeAnalyzerService
    {
        public NaturalKey GetNaturalKey(Type runtimeType)
        {
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
    }
}
