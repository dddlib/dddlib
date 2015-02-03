// <copyright file="EntityTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using dddlib.Sdk.Configuration;

    internal class EntityTypeFactory
    {
        public EntityType Create(Type type, EntityConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            var naturalKeySelector = string.IsNullOrEmpty(configuration.NaturalKeyPropertyName)
                ? null
                : new NaturalKeySelector(type, configuration.NaturalKeyPropertyName);

            return new EntityType(type, naturalKeySelector, configuration.NaturalKeyStringEqualityComparer, configuration.Mappings ?? new MapperCollection());
        }
    }
}
