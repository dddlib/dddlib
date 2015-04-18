// <copyright file="DefaultNaturalKeySerializer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

////namespace dddlib.Persistence.Sdk
////{
////    using System;
////    using System.Collections.Generic;
////    using System.Web.Script.Serialization;
////    using dddlib.Runtime;
////    using dddlib.Sdk;

////    /// <summary>
////    /// Represents the default natural key serializer.
////    /// </summary>
////    /// <typeparam name="T">The type of natural key.</typeparam>
////    public class DefaultNaturalKeySerializer<T> : INaturalKeySerializer<T>
////    {
////        // TODO (Cameron): Scope per application. Somehow.
////        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

////        /// <summary>
////        /// Initializes a new instance of the <see cref="DefaultNaturalKeySerializer{T}"/> class.
////        /// </summary>
////        public DefaultNaturalKeySerializer()
////        {
////            // NOTE (Cameron): This serializer only supports types of ValueObject<T>.
////            if (typeof(T).IsSubclassOfRawGeneric(typeof(ValueObject<>)))
////            {
////                ////Serializer.RegisterConverters(new[] { new ValueObjectConverter(typeof(T)) });
////            }
////        }

////        /// <summary>
////        /// Serializes the specified natural key.
////        /// </summary>
////        /// <param name="naturalKey">The natural key.</param>
////        /// <returns>The serialized natural key.</returns>
////        public string Serialize(T naturalKey)
////        {
////            return Serializer.Serialize(naturalKey);
////        }

////        /// <summary>
////        /// Deserializes the specified serialized natural key.
////        /// </summary>
////        /// <param name="serializedNaturalKey">The serialized natural key.</param>
////        /// <returns>The natural key.</returns>
////        public T Deserialize(string serializedNaturalKey)
////        {
////            return Serializer.Deserialize<T>(serializedNaturalKey);
////        }

////private class ValueObjectConverter : JavaScriptConverter
////{
////    private readonly Type[] supportedTypes;

////    public ValueObjectConverter(Type valueObjectType)
////    {
////        // TODO (Cameron): Serialize/deserialize function delegate or something similar.
////        this.supportedTypes = new[] { valueObjectType };
////    }

////    public override IEnumerable<Type> SupportedTypes
////    {
////        get { return this.supportedTypes; }
////    }

////    public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
////    {
////        ////if (type.GetConstructors().Wh)
////        ////{

////        ////}

////        ////var chosenConstructor = typeof(T).GetConstructors()
////        ////    .Where(constructor => constructor.GetParameters().All(parameter => !parameter.ParameterType.IsValueType))
////        ////    .OrderBy(constructor => constructor.GetParameters().Count())
////        ////    .FirstOrDefault();

////        ////var value = (string)dictionary["Value"];

////        return null;
////    }

////    public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
////    {
////        var dictionary = new Dictionary<string, object>();

////        foreach (var property in obj.GetType().GetProperties())
////        {
////            dictionary[property.Name] = property.GetValue(obj);
////        }
////        //// {"Value":"Key"}
////        ////var valueObject = (SensibleValueObject)obj;
////        ////dictionary["Value"] = valueObject.Value;
////        ////dictionary["Birthday"] = p.Birthday.ToString(_dateFormat);
////        return dictionary;
////    }
////}
////    }
////}
