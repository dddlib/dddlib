// <copyright file="IValueObjectSerializer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    /// <summary>
    /// Exposes the public members of the value object serializer.
    /// </summary>
    public interface IValueObjectSerializer
    {
        /// <summary>
        /// Serializes the specified value object.
        /// </summary>
        /// <param name="valueObject">The value object.</param>
        /// <returns>The serialized value object.</returns>
        string Serialize(object valueObject);

        /// <summary>
        /// Deserializes the specified serialized value object.
        /// </summary>
        /// <param name="serializedValueObject">The serialized value object.</param>
        /// <returns>The value object.</returns>
        object Deserialize(string serializedValueObject);
    }
}
