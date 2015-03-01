// <copyright file="MemoryIdentityMap.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a memory-based identity map.
    /// </summary>
    public class MemoryIdentityMap : IIdentityMap
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, Guid>> store = 
            new ConcurrentDictionary<Type, ConcurrentDictionary<object, Guid>>();

        /// <summary>
        /// Maps the specified key.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <param name="naturalKeyEqualityComparer">The natural key equality comparer.</param>
        /// <returns>
        /// A stream id.
        /// </returns>
        public Guid GetOrAdd(Type aggregateRootType, object naturalKey, IEqualityComparer<object> naturalKeyEqualityComparer)
        {
            var typeMappings = this.store.GetOrAdd(aggregateRootType, e => naturalKeyEqualityComparer == null ? new ConcurrentDictionary<object, Guid>() : new ConcurrentDictionary<object, Guid>(naturalKeyEqualityComparer));
            var id = typeMappings.GetOrAdd(naturalKey, Guid.NewGuid());

            return id;
        }

        /// <summary>
        /// Gets the specified key.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>
        /// A stream id.
        /// </returns>
        /// <exception cref="System.Exception">
        /// No such identity mapping type.
        /// or
        /// No such identity mapping ID.
        /// </exception>
        public Guid Get(Type aggregateRootType, object naturalKey)
        {
            var typeMappings = default(ConcurrentDictionary<object, Guid>);
            if (!this.store.TryGetValue(aggregateRootType, out typeMappings))
            {
                throw new Exception("No such identity mapping type.");
            }

            var id = default(Guid);
            if (!typeMappings.TryGetValue(naturalKey, out id))
            {
                throw new Exception("No such identity mapping ID.");
            }

            return id;
        }
    }
}
