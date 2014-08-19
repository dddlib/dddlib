// <copyright file="ConfigurationExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal static class ConfigurationExtensions
    {
        public static AggregateRootConfiguration Combine(this IEnumerable<AggregateRootConfiguration> values)
        {
            return new AggregateRootConfiguration
            {
                UninitializedFactory = values.Select(value => value.UninitializedFactory).CombineValues(),
                ////ApplyMethodName = values.Select(value => value.ApplyMethodName).CombineValues(),
                ////NaturalKeySelector = values.Select(value => value.NaturalKeySelector).CombineValues(),
                ////NaturalKeyEqualityComparer = values.Select(value => value.NaturalKeyEqualityComparer).CombineValues(),
            };
        }

        public static EntityConfiguration Combine(this IEnumerable<EntityConfiguration> values)
        {
            return new EntityConfiguration
            {
                EntityType = values.Select(value => value.EntityType).CombineValues(),
                NaturalKeyPropertyName = values.Select(value => value.NaturalKeyPropertyName).CombineValues(),
                NaturalKeyStringEqualityComparer = values.Select(value => value.NaturalKeyStringEqualityComparer).CombineValues(),
                ////NaturalKeySelector = values.Select(value => value.NaturalKeySelector).CombineValues(),
                ////NaturalKeyEqualityComparer = values.Select(value => value.NaturalKeyEqualityComparer).CombineValues(),
            };
        }

        public static ValueObjectConfiguration Combine(this IEnumerable<ValueObjectConfiguration> values)
        {
            return new ValueObjectConfiguration
            {
                EqualityComparer = values.Select(value => value.EqualityComparer).CombineValues(),
                ////Mapper = values.Select(value => value.Mapper).CombineValues(),
            };
        }

        private static T CombineValues<T>(this IEnumerable<T> values) where T : class
        {
            return CombineValues(values, value => value != null);
        }

        private static T? CombineValues<T>(this IEnumerable<T?> values) where T : struct
        {
            return CombineValues(values, value => value.HasValue);
        }

        private static T CombineValues<T>(IEnumerable<T> values, Func<T, bool> predicate)
        {
            var validValues = values.Where(predicate);

            if (!validValues.Any())
            {
                return default(T);
            }

            if (validValues.Distinct().Count() == 1)
            {
                return validValues.Distinct().Single();
            }

            throw new Exception("Invalid values.");
        }
    }
}
