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

        public IValueObjectConfigurationWrapper<T> ToMapAs<TOut>(Func<T, TOut> mapping)
        {
            // TODO (Cameron): Some expression based stuff here to negate the need to wrap.
            this.configuration.Mapper = type => mapping((T)type);
            return this;
        }

        public IValueObjectConfigurationWrapper<T> ToUseEqualityComparer(IEqualityComparer<T> equalityComparer)
        {
            this.configuration.EqaulityComparer = equalityComparer;
            return this;
        }
    }
}
