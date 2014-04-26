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
                Factory = values.Select(value => value.Factory).CombineValues(),
                ApplyMethodName = values.Select(value => value.ApplyMethodName).CombineValues(),
                ////NaturalKeySelector = values.Select(value => value.NaturalKeySelector).CombineValues(),
                ////NaturalKeyEqualityComparer = values.Select(value => value.NaturalKeyEqualityComparer).CombineValues(),
            };
        }

        public static EntityConfiguration Combine(this IEnumerable<EntityConfiguration> values)
        {
            return new EntityConfiguration
            {
                NaturalKeySelector = values.Select(value => value.NaturalKeySelector).CombineValues(),
                NaturalKeyEqualityComparer = values.Select(value => value.NaturalKeyEqualityComparer).CombineValues(),
                ////NaturalKeySelector = values.Select(value => value.NaturalKeySelector).CombineValues(),
                ////NaturalKeyEqualityComparer = values.Select(value => value.NaturalKeyEqualityComparer).CombineValues(),
            };
        }

        public static ValueObjectConfiguration Combine(this IEnumerable<ValueObjectConfiguration> values)
        {
            return new ValueObjectConfiguration
            {
                EqualityComparer = values.CombineValues(),
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
