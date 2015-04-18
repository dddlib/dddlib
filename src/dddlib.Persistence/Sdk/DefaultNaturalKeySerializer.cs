// <copyright file="DefaultNaturalKeySerializer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System.Web.Script.Serialization;
    using dddlib.Runtime;
    using dddlib.Sdk;

    /// <summary>
    /// Represents the default natural key serializer.
    /// </summary>
    public class DefaultNaturalKeySerializer : INaturalKeySerializer
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        /// <summary>
        /// Serializes the specified natural key.
        /// </summary>
        /// <typeparam name="T">The type of natural key.</typeparam>
        /// <param name="naturalKey">The natural key.</param>
        /// <returns>The serialized natural key.</returns>
        public string Serialize<T>(T naturalKey)
        {
            var naturalKeyType = naturalKey.GetType();
            if (naturalKeyType.IsSubclassOfRawGeneric(typeof(ValueObject<>)))
            {
                var runtimeType = Application.Current.GetValueObjectType(naturalKeyType);
                return runtimeType.Serializer.Serialize(naturalKey);
            }

            return Serializer.Serialize(naturalKey);
        }

        /// <summary>
        /// Deserializes the specified serialized natural key.
        /// </summary>
        /// <typeparam name="T">The type of natural key.</typeparam>
        /// <param name="serializedNaturalKey">The serialized natural key.</param>
        /// <returns>The natural key.</returns>
        public T Deserialize<T>(string serializedNaturalKey)
        {
            if (typeof(T).IsSubclassOfRawGeneric(typeof(ValueObject<>)))
            {
                var runtimeType = Application.Current.GetValueObjectType(typeof(T));
                return (T)runtimeType.Serializer.Deserialize(serializedNaturalKey);
            }

            return Serializer.Deserialize<T>(serializedNaturalKey);
        }
    }
}
