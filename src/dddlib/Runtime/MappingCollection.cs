// <copyright file="MappingCollection.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a collection of mappings.
    /// </summary>
    public sealed class MappingCollection
    {
        private static readonly MappingIdComparer Comparer = new MappingIdComparer();

        private readonly Dictionary<MappingId, object> mappings = new Dictionary<MappingId, object>(Comparer);

        /// <summary>
        /// Adds the or update.
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="mapping">The mapping.</param>
        public void AddOrUpdate<TSource, TDestination>(Func<TSource, TDestination> mapping)
        {
            Guard.Against.Null(() => mapping);

            var id = new MappingId { Source = typeof(TSource), Destination = typeof(TDestination), IsAction = false };

            this.mappings[id] = mapping;
        }

        /// <summary>
        /// Adds the or update.
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="mapping">The mapping.</param>
        public void AddOrUpdate<TSource, TDestination>(Action<TSource, TDestination> mapping)
        {
            Guard.Against.Null(() => mapping);

            var id = new MappingId { Source = typeof(TSource), Destination = typeof(TDestination), IsAction = true };

            this.mappings[id] = mapping;
        }

        /// <summary>
        /// Tries the get.
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="mapping">The mapping.</param>
        /// <returns>Returns <c>true</c> if the mapping exists, otherwise <c>false</c>.</returns>
        public bool TryGet<TSource, TDestination>(out Func<TSource, TDestination> mapping)
        {
            var id = new MappingId { Source = typeof(TSource), Destination = typeof(TDestination), IsAction = false };

            var value = default(object);
            if (!this.mappings.TryGetValue(id, out value))
            {
                mapping = null;
                return false;
            }

            mapping = value as Func<TSource, TDestination>;
            return true;
        }

        /// <summary>
        /// Tries the get.
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="mapping">The mapping.</param>
        /// <returns>Returns <c>true</c> if the mapping exists, otherwise <c>false</c>.</returns>
        public bool TryGet<TSource, TDestination>(out Action<TSource, TDestination> mapping)
        {
            var id = new MappingId { Source = typeof(TSource), Destination = typeof(TDestination), IsAction = true };

            var value = default(object);
            if (!this.mappings.TryGetValue(id, out value))
            {
                mapping = null;
                return false;
            }

            mapping = value as Action<TSource, TDestination>;
            return true;
        }

        private class MappingId
        {
            public Type Source { get; set; }

            public Type Destination { get; set; }

            public bool IsAction { get; set; }
        }

        private class MappingIdComparer : IEqualityComparer<MappingId>
        {
            public bool Equals(MappingId x, MappingId y)
            {
                return x.Source == y.Source && x.Destination == y.Destination && x.IsAction == y.IsAction;
            }

            public int GetHashCode(MappingId obj)
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 23) + obj.Source.GetHashCode();
                    hash = (hash * 23) + obj.Destination.GetHashCode();
                    hash = (hash * 23) + obj.IsAction.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
