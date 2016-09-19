// <copyright file="DefaultValueObjectSerializer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
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

        static DefaultValueObjectSerializer()
        {
            Serializer.RegisterConverters(new JavaScriptConverter[] { new DateTimeConverter(), new ValueObjectConverter() });
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

            [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
            public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
            {
                Guard.Against.Null(() => dictionary);

                var parameters = new Dictionary<string, object>(dictionary, StringComparer.OrdinalIgnoreCase);

                var constructors = typeof(T).GetConstructors()
                    .Where(ctor => ctor.GetParameters().Count() == dictionary.Count)
                    .Where(ctor => ctor.GetParameters().All(
                        parameter =>
                        {
                            object parameterValue;
                            return parameters.TryGetValue(parameter.Name, out parameterValue) &&
                                (parameterValue == null
                                    ? !parameter.ParameterType.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) != null
                                    : parameter.ParameterType.IsAssignableFrom(parameterValue.GetType()));
                        }))
                    .ToArray();

                if (constructors.Count() == 1)
                {
                    var constructor = constructors.SingleOrDefault();
                    return constructor.Invoke(constructor.GetParameters().Select(parameter => parameters[parameter.Name]).ToArray());
                }

                var defaultConstructor = typeof(T).GetConstructor(Type.EmptyTypes);
                if (defaultConstructor == null)
                {
                    throw new RuntimeException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            @"Unable to deserialize value object of type '{0}' using the default value object serializer as there is no suitable constructor defined.
To fix this issue, either:
- add a single public constructor that accepts the following parameter(s): '{1}', or
- define a custom serializer in a bootstrapper.",
                            typeof(T),
                            string.Join(", ", dictionary.Keys)))
                    {
                        HelpLink = "https://github.com/dddlib/dddlib/wiki/Value-Object-Serialization",
                    };
                }

                var valueObject = Activator.CreateInstance<T>();
                foreach (var property in valueObject.GetType().GetProperties().Where(property => property.CanWrite))
                {
                    property.SetValue(valueObject, dictionary[property.Name]);
                }

                return valueObject;
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
