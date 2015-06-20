// <copyright file="INaturalKeySerializer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;

    /// <summary>
    /// Exposes the public members of the natural key serializer.
    /// </summary>
    public interface INaturalKeySerializer
    {
        /// <summary>
        /// Serializes the specified natural key.
        /// </summary>
        /// <param name="naturalKeyType">The type of natural key.</param>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The serialized natural key.</returns>
        string Serialize(Type naturalKeyType, object naturalKey);

        /// <summary>
        /// Deserializes the specified serialized natural key.
        /// </summary>
        /// <param name="naturalKeyType">The type of natural key.</param>
        /// <param name="serializedNaturalKey">The serialized natural key.</param>
        /// <returns>The natural key.</returns>
        object Deserialize(Type naturalKeyType, string serializedNaturalKey);
    }
}
