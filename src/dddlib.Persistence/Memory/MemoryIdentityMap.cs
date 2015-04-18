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
        /// Maps the specified key.
        /// </summary>
        /// <typeparam name="T">The type of natural key.</typeparam>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>
        /// A stream id.
        /// </returns>
        public Guid GetOrAdd<T>(Type aggregateRootType, T naturalKey)
        {
            var mappings = this.store.GetOrAdd(aggregateRootType, _ => new ConcurrentDictionary<object, Guid>());
            var id = mappings.GetOrAdd(naturalKey, Guid.NewGuid());

            return id;
        }

        /// <summary>
        /// Gets the specified key.
        /// </summary>
        /// <typeparam name="T">The type of natural key.</typeparam>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <param name="identity">The mapped identity.</param>
        /// <returns>
        /// A stream id.
        /// </returns>
        /// <exception cref="System.Exception">No such identity mapping type.
        /// or
        /// No such identity mapping ID.</exception>
        public bool TryGet<T>(Type aggregateRootType, T naturalKey, out Guid identity)
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
