// <copyright file="ValueObjectTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal class ValueObjectTypeFactory : ITypeFactory<ValueObjectType>
    {
        private readonly IConfigurationProvider<ValueObjectConfiguration> configurationProvider;

        public ValueObjectTypeFactory(IConfigurationProvider<ValueObjectConfiguration> configurationProvider)
        {
            Guard.Against.Null(() => configurationProvider);

            this.configurationProvider = configurationProvider;
        }

        public ValueObjectType Create(Type type)
        {
            var configuration = this.configurationProvider.GetConfiguration(type);

            // create type
            return new ValueObjectType(configuration.EqaulityComparer);
        }
    }
}
