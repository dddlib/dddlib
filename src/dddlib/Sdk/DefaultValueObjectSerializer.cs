// <copyright file="DefaultValueObjectSerializer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.Script.Serialization;
    using dddlib.Runtime;

    /// <summary>
    /// Represents the default value object serializer.
    /// </summary>
    /// <typeparam name="T">The type of value object.</typeparam>
    public class DefaultValueObjectSerializer<T> : IValueObjectSerializer
        where T : ValueObject<T>
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultValueObjectSerializer{T}"/> class.
        /// </summary>
        public DefaultValueObjectSerializer()
        {
            Serializer.RegisterConverters(new[] { new ValueObjectConverter() });
        }

        /// <summary>
        /// Serializes the specified value object.
        /// </summary>
        /// <param name="valueObject">The value object.</param>
        /// <returns>The serialized value object.</returns>
        public string Serialize(object valueObject)
        {
            return Serializer.Serialize(valueObject);
        }

        /// <summary>
        /// Deserializes the specified serialized value object.
        /// </summary>
        /// <param name="serializedValueObject">The serialized value object.</param>
        /// <returns>The value object.</returns>
        public object Deserialize(string serializedValueObject)
        {
            return Serializer.Deserialize<T>(serializedValueObject);
        }

        private class ValueObjectConverter : JavaScriptConverter
        {
            public override IEnumerable<Type> SupportedTypes
            {
                get { return new[] { typeof(T) }; }
            }

            public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
            {
                var constructor = typeof(T).GetConstructors().SingleOrDefault(ctor => ctor.GetParameters().Count() == dictionary.Count);
                if (constructor == null)
                {
                    var defaultConstructor = typeof(T).GetConstructor(Type.EmptyTypes);
                    if (defaultConstructor == null)
                    {
                        // TODO (Cameron): Fix exception.
                        throw new RuntimeException("deserialization error!");
                    }

                    var valueObject = Activator.CreateInstance<T>();
                    foreach (var property in valueObject.GetType().GetProperties().Where(property => property.CanWrite))
                    {
                        property.SetValue(valueObject, dictionary[property.Name]);
                    }

                    return valueObject;
                }

                return constructor.Invoke(dictionary.Select(kvp => kvp.Value).ToArray());
            }

            public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
            {
                return obj.GetType()
                    .GetProperties()
                    .ToDictionary(property => property.Name, property => property.GetValue(obj));
            }
        }
    }
}
