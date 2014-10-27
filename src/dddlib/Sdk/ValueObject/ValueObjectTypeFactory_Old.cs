// <copyright file="ValueObjectTypeFactory_Old.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal class ValueObjectTypeFactory_Old : ITypeFactory<ValueObjectType>
    {
        private readonly ValueObjectTypeFactory factory = new ValueObjectTypeFactory();
        private readonly IValueObjectConfigurationProvider configurationProvider;

        public ValueObjectTypeFactory_Old(IValueObjectConfigurationProvider configurationProvider)
        {
            Guard.Against.Null(() => configurationProvider);

            this.configurationProvider = configurationProvider;
        }

        public ValueObjectType Create(Type type)
        {
            var configuration = this.configurationProvider.GetConfiguration(type);
            configuration.RuntimeType = type;

            // create type
            return this.factory.Create(configuration);
        }
    }
}
