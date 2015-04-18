// <copyright file="IIdentityMap.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    using System;

    /// <summary>
    /// Exposes the public members of the identity map.
    /// </summary>
    public interface IIdentityMap
    {
        /// <summary>
        /// Gets the mapped identity for the specified natural key. If a mapping does not exist then one is created.
        /// </summary>
        /// <typeparam name="T">The type of natural key.</typeparam>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The mapped identity.</returns>
        Guid GetOrAdd<T>(Type aggregateRootType, T naturalKey);

        /// <summary>
        /// Attempts to get the mapped identity for the specified natural key.
        /// </summary>
        /// <typeparam name="T">The type of natural key.</typeparam>
        /// <param name="aggregateRootType">Type of the aggregate root.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <param name="identity">The mapped identity.</param>
        /// <returns>Returns <c>true</c> if the mapping exists; otherwise <c>false</c>.</returns>
        bool TryGet<T>(Type aggregateRootType, T naturalKey, out Guid identity);
    }
}
