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

        public ValueObjectConfigurationWrapper(ValueObjectConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            this.configuration = configuration;
        }

        public IValueObjectConfigurationWrapper<T> ToUseEqualityComparer(IEqualityComparer<T> equalityComparer)
        {
            Guard.Against.Null(() => equalityComparer);

            this.configuration.EqualityComparer = equalityComparer;

            return this;
        }

        public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<TEvent, T> mapping)
        {
            Guard.Against.Null(() => mapping);

            // TODO (Cameron): Some expression based stuff here to negate the need to wrap.
            ////this.configuration.ToEventMapping = (@event, valueObject) => mapping((TEvent)@event, (T)valueObject);

            return this;
        }

        public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<TEvent, T> mapping, Func<TEvent, T> reverseMapping)
        {
            Guard.Against.Null(() => mapping);
            Guard.Against.Null(() => reverseMapping);

            // TODO (Cameron): Some expression based stuff here to negate the need to wrap.
            ////this.configuration.ToEventMapping = (@event, valueObject) => mapping((TEvent)@event, (T)valueObject);
            ////this.configuration.FromEventMapping = @event => reverseMapping((TEvent)@event);

            return this;
        }
    }
}
