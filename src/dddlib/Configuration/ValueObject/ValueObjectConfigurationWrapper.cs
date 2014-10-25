// <copyright file="ValueObjectConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using dddlib.Runtime;

    internal class ValueObjectConfigurationWrapper<T> : IValueObjectConfigurationWrapper<T>
        where T : ValueObject<T>
    {
        private readonly ValueObjectConfiguration configuration;
        private readonly Mapper mapper;

        public ValueObjectConfigurationWrapper(ValueObjectConfiguration configuration, Mapper mapper)
        {
            Guard.Against.Null(() => configuration);
            Guard.Against.Null(() => mapper);

            this.configuration = configuration;
            this.mapper = mapper;
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

            this.mapper.AddMap(mapping);

            return this;
        }

        public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping)
        {
            Guard.Against.Null(() => mapping);
            Guard.Against.Null(() => reverseMapping);

            this.mapper.AddMap(mapping);
            this.mapper.AddMap(reverseMapping);

            return this;
        }
    }
}
