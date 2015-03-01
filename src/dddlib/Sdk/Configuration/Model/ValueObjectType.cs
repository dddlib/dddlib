// <copyright file="ValueObjectType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Model
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using dddlib.Sdk.Configuration.Model.TypeAnalyzerService;

    internal class ValueObjectType : Entity
    {
        private static readonly ITypeAnalyzerService DefaultTypeAnalyzerService = new DefaultTypeAnalyzerService();

        public ValueObjectType(Type runtimeType, ITypeAnalyzerService typeAnalyzerService)
              : base(new NaturalKey(typeof(ValueObjectType), "RuntimeType", typeof(Type), DefaultTypeAnalyzerService), EqualityComparer<object>.Default)
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
            this.Mappings = new MapperCollection();
        }

        [dddlib.NaturalKey]
        public Type RuntimeType { get; private set; }

        public object EqualityComparer { get; private set; }

        // TODO (Cameron): This is not right. Law of Dementer and all that.
        public MapperCollection Mappings { get; private set; }

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
    }
}
