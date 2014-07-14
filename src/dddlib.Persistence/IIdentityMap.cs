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
        /// Maps the specified key.
        /// </summary>
        /// <param name="type">The type of aggregate root.</param>
        /// <param name="key">The key value.</param>
        /// <returns>A stream id.</returns>
        Guid Map(Type type, object key);
    }
}
