// <copyright file="EntityTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using dddlib.Sdk;

    internal class EntityTypeFactory : IEntityTypeFactory
    {
        public EntityType Create(EntityConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            var naturalKeySelector = string.IsNullOrEmpty(configuration.NaturalKeyPropertyName)
                ? null
                : new NaturalKeySelector(configuration.RuntimeType, configuration.NaturalKeyPropertyName);

            return new EntityType(configuration.RuntimeType, naturalKeySelector, configuration.NaturalKeyStringEqualityComparer);
        }
    }
}
