// <copyright file="INaturalKeySerializer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    /// <summary>
    /// Exposes the public members of the natural key serializer.
    /// </summary>
    public interface INaturalKeySerializer
    {
        /// <summary>
        /// Serializes the specified natural key.
        /// </summary>
        /// <typeparam name="T">The type of natural key.</typeparam>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The serialized natural key.</returns>
        string Serialize<T>(T naturalKey);

        /// <summary>
        /// Deserializes the specified serialized natural key.
        /// </summary>
        /// <typeparam name="T">The type of natural key.</typeparam>
        /// <param name="serializedNaturalKey">The serialized natural key.</param>
        /// <returns>The natural key.</returns>
        T Deserialize<T>(string serializedNaturalKey);
    }
}
