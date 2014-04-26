// <copyright file="ValueObjectTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class ValueObjectTypeFactory
    {
        private readonly ValueObjectConfigurationProvider configurationProvider;

        public ValueObjectTypeFactory(ValueObjectConfigurationProvider configurationProvider)
        {
            Guard.Against.Null(() => configurationProvider);

            this.configurationProvider = configurationProvider;
        }

        public ValueObjectType Create(Type type)
        {
            var configuration = this.configurationProvider.Get(type);

            // create type
            return new ValueObjectType
            {
                EqualityComparer = configuration.EqualityComparer as IEqualityComparer<object>,
            };
        }
    }
}
