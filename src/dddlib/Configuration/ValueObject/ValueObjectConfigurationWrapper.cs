// <copyright file="ValueObjectConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using dddlib.Runtime;

    internal class ValueObjectConfigurationWrapper<T>(private ValueObjectConfiguration configuration) : IValueObjectConfigurationWrapper<T>
        where T : ValueObject<T>
    {
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
            ////this.configuration.Mapper = type => mapping((T)type);
            return this;
        }

        public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<TEvent, T> mapping, Func<TEvent, T> reverseMapping)
        {
            Guard.Against.Null(() => mapping);
            Guard.Against.Null(() => reverseMapping);

            // TODO (Cameron): Some expression based stuff here to negate the need to wrap.
            ////this.configuration.Mapper = type => mapping((T)type);
            return this;
        }
    }
}
