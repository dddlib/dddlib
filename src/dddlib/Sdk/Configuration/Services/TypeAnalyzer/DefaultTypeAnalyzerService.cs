// <copyright file="DefaultTypeAnalyzerService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Services.TypeAnalyzer
{
    using System;
    using System.Linq;
    using System.Reflection;
    using dddlib.Sdk.Configuration.Model;

    internal class DefaultTypeAnalyzerService : ITypeAnalyzerService
    {
        public NaturalKey GetNaturalKey(Type runtimeType)
        {
            var naturalKey = runtimeType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(member => member.GetCustomAttributes(typeof(dddlib.NaturalKey), false).SingleOrDefault() != null)
                .SingleOrDefault();

            return naturalKey == null
                ? null
                : new NaturalKey(naturalKey.DeclaringType, naturalKey.Name, naturalKey.PropertyType, this);
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
