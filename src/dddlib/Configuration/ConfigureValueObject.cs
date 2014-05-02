// <copyright file="ConfigureValueObject.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using dddlib.Runtime;

    internal class ConfigureValueObject<T> : IConfigureValueObject<T>
        where T : ValueObject<T>
    {
        private readonly ValueObjectConfiguration configuration;

        public ConfigureValueObject(ValueObjectConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            this.configuration = configuration;
        }

        public IConfigureValueObject<T> ToMapAs<TOut>(Func<T, TOut> mapping)
        {
            // TODO (Cameron): Some expression based stuff here to negate the need to wrap.
            this.configuration.Mapper = type => mapping((T)type);
            return this;
        }
    }
}
