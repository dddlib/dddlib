// <copyright file="ValueObjectType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Model
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using dddlib.Runtime;
    using dddlib.Sdk.Configuration.Services.TypeAnalyzer;

    /// <summary>
    /// Represents a value object type.
    /// </summary>
    public class ValueObjectType : Entity
    {
        private static readonly ITypeAnalyzerService DefaultTypeAnalyzerService = new DefaultTypeAnalyzerService();

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueObjectType"/> class.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <param name="typeAnalyzerService">The type analyzer service.</param>
        public ValueObjectType(Type runtimeType, ITypeAnalyzerService typeAnalyzerService)
              : base(new NaturalKey(typeof(ValueObjectType), "RuntimeType", typeof(Type), DefaultTypeAnalyzerService))
        {
            Guard.Against.Null(() => runtimeType);
            Guard.Against.Null(() => typeAnalyzerService);

            if (!typeAnalyzerService.IsValidValueObject(runtimeType))
            {
                throw new BusinessException(
                    string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not a value object.", runtimeType));
            }

            this.RuntimeType = runtimeType;
            this.EqualityComparer = Activator.CreateInstance(typeof(DefaultValueObjectEqualityComparer<>).MakeGenericType(runtimeType));
            this.Serializer = (IValueObjectSerializer)Activator.CreateInstance(typeof(DefaultValueObjectSerializer<>).MakeGenericType(runtimeType));
            this.Mappings = new MapperCollection();
        }

        /// <summary>
        /// Gets the runtime type for this value object type.
        /// </summary>
        /// <value>The runtime type.</value>
        [dddlib.NaturalKey]
        public Type RuntimeType { get; private set; }

        /// <summary>
        /// Gets the equality comparer for this value object type.
        /// </summary>
        /// <value>The equality comparer.</value>
        public object EqualityComparer { get; private set; }

        /// <summary>
        /// Gets the serializer for this value object type.
        /// </summary>
        /// <value>The serializer.</value>
        public IValueObjectSerializer Serializer { get; private set; }

        /// <summary>
        /// Gets the mappings.
        /// </summary>
        /// <value>The mappings.</value>
        //// TODO (Cameron): This is not right. Law of Dementer and all that.
        public MapperCollection Mappings { get; private set; }

        /// <summary>
        /// Configures the equality comparer for this value object type.
        /// </summary>
        /// <typeparam name="T">The value object type.</typeparam>
        /// <param name="equalityComparer">The equality comparer.</param>
        public void ConfigureEqualityComparer<T>(IEqualityComparer<T> equalityComparer)
        {
            Guard.Against.Null(() => equalityComparer);

            var equalityComparerType = typeof(IEqualityComparer<>).MakeGenericType(this.RuntimeType);
            if (!equalityComparerType.IsAssignableFrom(equalityComparer.GetType()))
            {
                throw new BusinessException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid equality comparer. The specified equality comparer of type '{0}' does not match the required type of '{1}'.",
                        equalityComparer.GetType(),
                        equalityComparerType));
            }

            this.EqualityComparer = equalityComparer;
        }

        /// <summary>
        /// Configures the serializer for this value object type.
        /// </summary>
        /// <param name="valueObjectSerializer">The value object serializer.</param>
        public void ConfigureSerializer(IValueObjectSerializer valueObjectSerializer)
        {
            Guard.Against.Null(() => valueObjectSerializer);

            ////var equalityComparerType = typeof(IEqualityComparer<>).MakeGenericType(this.RuntimeType);
            ////if (!equalityComparerType.IsAssignableFrom(equalityComparer.GetType()))
            ////{
            ////    throw new BusinessException(
            ////        string.Format(
            ////            CultureInfo.InvariantCulture,
            ////            "Invalid equality comparer. The specified equality comparer of type '{0}' does not match the required type of '{1}'.",
            ////            equalityComparer.GetType(),
            ////            equalityComparerType));
            ////}

            this.Serializer = valueObjectSerializer;
        }
    }
}
