// <copyright file="EntityTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using dddlib.Sdk;

    internal class EntityTypeFactory : ITypeFactory<EntityType>
    {
        private readonly IEntityConfigurationProvider configurationProvider;

        public EntityTypeFactory(IEntityConfigurationProvider configurationProvider)
        {
            Guard.Against.Null(() => configurationProvider);

            this.configurationProvider = configurationProvider;
        }

        public EntityType Create(Type type)
        {
            var configuration = this.configurationProvider.GetConfiguration(type);

            var naturalKeySelector = string.IsNullOrEmpty(configuration.NaturalKeyPropertyName)
                ? null
                : new NaturalKeySelector(configuration.EntityType, configuration.NaturalKeyPropertyName);

            // create type
            return new EntityType(type, naturalKeySelector, configuration.NaturalKeyStringEqualityComparer);
        }
    }
}
