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
        /// <param name="type">The type of aggregate root.</param>
        /// <param name="key">The key value.</param>
        /// <returns>A stream id.</returns>
        public Guid Map(Type type, object key)
        {
            var typeMappings = this.store.GetOrAdd(type, new ConcurrentDictionary<object, Guid>());

            return typeMappings.GetOrAdd(key, Guid.NewGuid());
        }
    }
}
