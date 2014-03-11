// <copyright file="EntityAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Analyzer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    internal class EntityAnalyzer
    {
        public EntityType GetRuntimeType(Type type, EntityConfiguration configuration)
        {
            if (!typeof(Entity).IsAssignableFrom(type))
            {
                throw new Exception();
            }

            var naturalKey = default(NaturalKey);
            foreach (var subType in new[] { type }.Traverse(t => t.BaseType == typeof(Entity) ? null : new[] { t.BaseType }))
            {
                naturalKey = subType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                    .SelectMany(member => member.GetCustomAttributes(typeof(NaturalKey), true))
                    .OfType<NaturalKey>()
                    .SingleOrDefault();

                if (naturalKey != null)
                {
                    break;
                }
            }

            var naturalKeyEqualityComparer = default(NaturalKey.EqualityComparer);
            foreach (var subType in new[] { type }.Traverse(t => t.BaseType == typeof(Entity) ? null : new[] { t.BaseType }))
            {
                naturalKeyEqualityComparer = subType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                    .SelectMany(member => member.GetCustomAttributes(typeof(NaturalKey.EqualityComparer), true))
                    .OfType<NaturalKey.EqualityComparer>()
                    .SingleOrDefault();

                if (naturalKeyEqualityComparer != null)
                {
                    break;
                }
            }

            if (naturalKey == null) 
            {
                if (naturalKeyEqualityComparer != null)
                {
                    // NOTE (Cameron): There is an equality comparer defined without a natural key.
                    throw new Exception();
                }

                // TODO (Cameron): Move into AggregateRootTypeAnalyzer.
                ////if (configuration.AggregateRootFactory != null)
                ////{
                ////    throw new RuntimeException(
                ////        string.Format(CultureInfo.InvariantCulture, "The entity of type '{0}' does not have a natural key defined.", type.Name));
                ////}

                return new EntityType();
            }

            // TODO (Cameron): Get equality comparer from config.
            var runtimeType = new EntityType
            {
                EqualityComparer = EqualityComparer<object>.Default,
            };

            return runtimeType;
        }
    }
}
