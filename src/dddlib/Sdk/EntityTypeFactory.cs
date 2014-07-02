// <copyright file="EntityTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using dddlib.Sdk;

    internal class EntityTypeFactory : ITypeFactory<EntityType>
    {
        private readonly IConfigurationProvider<EntityConfiguration> configurationProvider;

        public EntityTypeFactory(IConfigurationProvider<EntityConfiguration> configurationProvider)
        {
            Guard.Against.Null(() => configurationProvider);

            this.configurationProvider = configurationProvider;
        }

        public EntityType Create(Type type)
        {
            var configuration = this.configurationProvider.GetConfiguration(type);

            var naturalKeySelector = new NaturalKeySelector(configuration.NaturalKeyPropertyName, configuration.EntityType);

            // create type
            return new EntityType(naturalKeySelector, configuration.NaturalKeyStringEqualityComparer);
        }
    }
}
