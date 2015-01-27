// <copyright file="ValueObjectConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Collections.Generic;
    using dddlib.Configuration;
    using dddlib.Sdk;

    internal class ValueObjectConfigurationWrapper<T> : IValueObjectConfigurationWrapper<T>
        where T : ValueObject<T>
    {
        private readonly ValueObjectConfiguration configuration;

        public ValueObjectConfigurationWrapper(ValueObjectConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            this.configuration = configuration;

            // TODO (Cameron): Not sure this belongs here...
            if (this.configuration.Mappings == null)
            {
                this.configuration.Mappings = new MappingCollection();
            }
        }

        public IValueObjectConfigurationWrapper<T> ToUseEqualityComparer(IEqualityComparer<T> equalityComparer)
        {
            Guard.Against.Null(() => equalityComparer);

            this.configuration.EqualityComparer = equalityComparer;

            return this;
        }

        public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping)
        {
            Guard.Against.Null(() => mapping);

            this.configuration.Mappings.AddOrUpdate(mapping);
            ////this.mapper.AddMap(mapping);

            return this;
        }

        public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping)
        {
            Guard.Against.Null(() => mapping);
            Guard.Against.Null(() => reverseMapping);

            this.configuration.Mappings.AddOrUpdate(mapping);
            this.configuration.Mappings.AddOrUpdate(reverseMapping);
            ////this.mapper.AddMap(mapping);
            ////this.mapper.AddMap(reverseMapping);

            return this;
        }
    }
}
