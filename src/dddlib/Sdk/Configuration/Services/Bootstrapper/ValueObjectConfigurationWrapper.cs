// <copyright file="ValueObjectConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Services.Bootstrapper
{
    using System;
    using System.Collections.Generic;
    using dddlib.Configuration;
    using dddlib.Sdk;
    using dddlib.Sdk.Configuration.Model;

    internal class ValueObjectConfigurationWrapper<T> : IValueObjectConfigurationWrapper<T>
        where T : ValueObject<T>
    {
        private readonly ValueObjectType valueObjectType;

        public ValueObjectConfigurationWrapper(ValueObjectType valueObjectType)
        {
            Guard.Against.Null(() => valueObjectType);

            this.valueObjectType = valueObjectType;
        }

        public IValueObjectConfigurationWrapper<T> ToUseEqualityComparer(IEqualityComparer<T> equalityComparer)
        {
            Guard.Against.Null(() => equalityComparer);

            this.valueObjectType.ConfigureEqualityComparer(equalityComparer);

            return this;
        }

        public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping)
        {
            Guard.Against.Null(() => mapping);

            this.valueObjectType.Mappings.AddOrUpdate(mapping);

            return this;
        }

        public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping)
        {
            Guard.Against.Null(() => mapping);
            Guard.Against.Null(() => reverseMapping);

            this.valueObjectType.Mappings.AddOrUpdate(mapping);
            this.valueObjectType.Mappings.AddOrUpdate(reverseMapping);

            return this;
        }
    }
}
