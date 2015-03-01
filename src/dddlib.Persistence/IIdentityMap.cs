// <copyright file="IIdentityMap.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Exposes the public members of the identity map.
    /// </summary>
    public interface IIdentityMap
    {
        /// <summary>
        /// Maps the specified key.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <param name="naturalKeyEqualityComparer">The natural key equality comparer.</param>
        /// <returns>A stream id.</returns>
        Guid GetOrAdd(Type aggregateRootType, object naturalKey, IEqualityComparer<object> naturalKeyEqualityComparer);

        /// <summary>
        /// Gets the specified type.
        /// </summary>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>A stream id.</returns>
        Guid Get(Type aggregateRootType, object naturalKey);
    }
}
