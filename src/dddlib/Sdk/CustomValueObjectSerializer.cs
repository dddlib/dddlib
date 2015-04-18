// <copyright file="CustomValueObjectSerializer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using dddlib.Runtime;

    /// <summary>
    /// Represents a delegate based natural key serializer.
    /// </summary>
    /// <typeparam name="T">The type of natural key.</typeparam>
    public class CustomValueObjectSerializer<T> : IValueObjectSerializer
        where T : ValueObject<T>
    {
        private readonly Func<T, string> serialize;
        private readonly Func<string, T> deserialize;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomValueObjectSerializer{T}" /> class.
        /// </summary>
        /// <param name="serialize">The serialization delegate.</param>
        /// <param name="deserialize">The deserialization delegate.</param>
        public CustomValueObjectSerializer(Func<T, string> serialize, Func<string, T> deserialize)
        {
            Guard.Against.Null(() => serialize);
            Guard.Against.Null(() => deserialize);

            this.serialize = serialize;
            this.deserialize = deserialize;
        }

        /// <summary>
        /// Serializes the specified natural key.
        /// </summary>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The serialized natural key.</returns>
        public string Serialize(T naturalKey)
        {
            return this.serialize(naturalKey);
        }

        /// <summary>
        /// Deserializes the specified serialized natural key.
        /// </summary>
        /// <param name="serializedNaturalKey">The serialized natural key.</param>
        /// <returns>The natural key.</returns>
        public T Deserialize(string serializedNaturalKey)
        {
            return this.deserialize(serializedNaturalKey);
        }

        string IValueObjectSerializer.Serialize(object valueObject)
        {
            return this.Serialize((T)valueObject);
        }

        object IValueObjectSerializer.Deserialize(string serializedValueObject)
        {
            return this.Deserialize(serializedValueObject);
        }
    }
}
