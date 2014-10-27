// <copyright file="EntityTypeFactory_Old.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using dddlib.Sdk;

    internal class EntityTypeFactory_Old : ITypeFactory<EntityType>
    {
        private readonly EntityTypeFactory factory = new EntityTypeFactory();
        private readonly IEntityConfigurationProvider configurationProvider;

        public EntityTypeFactory_Old(IEntityConfigurationProvider configurationProvider)
        {
            Guard.Against.Null(() => configurationProvider);

            this.configurationProvider = configurationProvider;
        }

        public EntityType Create(Type type)
        {
            var configuration = this.configurationProvider.GetConfiguration(type);
            configuration.RuntimeType = type;

            // TODO (Cameron): Consider moving some of this into the type itself.
            return this.factory.Create(configuration);
        }
    }
}
