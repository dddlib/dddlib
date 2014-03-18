// <copyright file="EntityTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class EntityTypeFactory
    {
        private readonly EntityConfigurationProvider configurationProvider;

        public EntityTypeFactory(EntityConfigurationProvider configurationProvider)
        {
            Guard.Against.Null(() => configurationProvider);

            this.configurationProvider = configurationProvider;
        }

        public EntityType Create(Type type)
        {
            var configuration = this.configurationProvider.Get(type);

            // create type
            return new EntityType
            {
                NaturalKeySelector = configuration.NaturalKeySelector,
                NaturalKeyEqualityComparer = configuration.NaturalKeyEqualityComparer,
            };
        }
    }
}
