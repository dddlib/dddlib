// <copyright file="MemoryIdentityMap.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Represents a memory-based identity map.
    /// </summary>
    public class MemoryIdentityMap : IIdentityMap
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, Guid>> store = 
            new ConcurrentDictionary<Type, ConcurrentDictionary<object, Guid>>();

        /// <summary>
        /// Gets the mapped identity for the specified natural key. If a mapping does not exist then one is created.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKeyType">Type of the natural key.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The mapped identity.</returns>
        public Guid GetOrAdd(Type aggregateRootType, Type naturalKeyType, object naturalKey)
        {
            var mappings = this.store.GetOrAdd(aggregateRootType, _ => new ConcurrentDictionary<object, Guid>());
            var id = mappings.GetOrAdd(naturalKey, Guid.NewGuid());

            return id;
        }

        /// <summary>
        /// Attempts to get the mapped identity for the specified natural key.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKeyType">Type of the natural key.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <param name="identity">The mapped identity.</param>
        /// <returns>Returns <c>true</c> if the mapping exists; otherwise <c>false</c>.</returns>
        public bool TryGet(Type aggregateRootType, Type naturalKeyType, object naturalKey, out Guid identity)
        {
            var typeMappings = default(ConcurrentDictionary<object, Guid>);
            if (!this.store.TryGetValue(aggregateRootType, out typeMappings))
            {
                return false;
            }

            return typeMappings.TryGetValue(naturalKey, out identity);
        }
    }
}
