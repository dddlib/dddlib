// <copyright file="EntityConfigurationManager.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using dddlib.Sdk;

    internal class EntityConfigurationManager : IConfigurationManager<EntityConfiguration>
    {
        public EntityConfiguration Merge(EntityConfiguration typeConfiguration, EntityConfiguration baseTypeConfiguration)
        {
            // this merges the base type configuration
            // there is logic required in here
            return new EntityConfiguration
            {
                RuntimeType = typeConfiguration.RuntimeType ?? baseTypeConfiguration.RuntimeType,
                NaturalKeyPropertyName = typeConfiguration.NaturalKeyPropertyName ?? baseTypeConfiguration.NaturalKeyPropertyName,
                NaturalKeyStringEqualityComparer = typeConfiguration.NaturalKeyStringEqualityComparer ?? baseTypeConfiguration.NaturalKeyStringEqualityComparer,
                Mappings = typeConfiguration.Mappings ?? baseTypeConfiguration.Mappings, // TODO (Cameron): Not right.
            };
        }
    }
}
