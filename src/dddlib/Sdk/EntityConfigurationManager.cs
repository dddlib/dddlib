// <copyright file="EntityConfigurationManager.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    internal class EntityConfigurationManager : IConfigurationManager<EntityConfiguration>
    {
        public EntityConfiguration Merge(EntityConfiguration typeConfiguration, EntityConfiguration baseTypeConfiguration)
        {
            // this merges the base type configuration
            // there is logic required in here
            return new EntityConfiguration
            {
                NaturalKeySelector = typeConfiguration.NaturalKeySelector ?? baseTypeConfiguration.NaturalKeySelector,
                NaturalKeyStringEqualityComparer = typeConfiguration.NaturalKeyStringEqualityComparer ?? baseTypeConfiguration.NaturalKeyStringEqualityComparer,
            };
        }
    }
}
