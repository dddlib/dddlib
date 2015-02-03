// <copyright file="EntityConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Collections.Generic;
    using dddlib.Runtime;

    /// <summary>
    /// Represents the entity configuration.
    /// </summary>
    public class EntityConfiguration
    {
        /// <summary>
        /// Gets or sets the name of the natural key property.
        /// </summary>
        /// <value>The name of the natural key property.</value>
        public string NaturalKeyPropertyName { get; set; }

        /// <summary>
        /// Gets or sets the natural key string equality comparer.
        /// </summary>
        /// <value>The natural key string equality comparer.</value>
        public IEqualityComparer<string> NaturalKeyStringEqualityComparer { get; set; }

        /// <summary>
        /// Gets or sets the mappings for this entity.
        /// </summary>
        /// <value>The mappings for this entity.</value>
        public MapperCollection Mappings { get; set; }

        /// <summary>
        /// Combines the specified primary and secondary entity configurations.
        /// </summary>
        /// <param name="primaryConfiguration">The primary configuration.</param>
        /// <param name="secondaryConfiguration">The secondary configuration.</param>
        /// <returns>The combined entity configuration.</returns>
        public static EntityConfiguration Combine(EntityConfiguration primaryConfiguration, EntityConfiguration secondaryConfiguration)
        {
            Guard.Against.Null(() => primaryConfiguration);
            Guard.Against.Null(() => secondaryConfiguration);

            if (primaryConfiguration.NaturalKeyPropertyName != null &&
                secondaryConfiguration.NaturalKeyPropertyName != null &
                !string.Equals(
                    primaryConfiguration.NaturalKeyPropertyName, 
                    secondaryConfiguration.NaturalKeyPropertyName, 
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("Configured natural key property names do not match.");
            }

            // TODO (Cameron): Check natural key property names match.
            // TODO (Cameron): Ensure equality comparers match.
            return new EntityConfiguration
            {
                NaturalKeyPropertyName = primaryConfiguration.NaturalKeyPropertyName ?? secondaryConfiguration.NaturalKeyPropertyName,
                NaturalKeyStringEqualityComparer = primaryConfiguration.NaturalKeyStringEqualityComparer ?? secondaryConfiguration.NaturalKeyStringEqualityComparer,
                Mappings = primaryConfiguration.Mappings ?? secondaryConfiguration.Mappings, // TODO (Cameron): This is wrong.
            };
        }

        /// <summary>
        /// Merges the specified type and base type entity configurations.
        /// </summary>
        /// <param name="typeConfiguration">The type configuration.</param>
        /// <param name="baseTypeConfiguration">The base type configuration.</param>
        /// <returns>The merged entity configuration.</returns>
        public static EntityConfiguration Merge(EntityConfiguration typeConfiguration, EntityConfiguration baseTypeConfiguration)
        {
            Guard.Against.Null(() => typeConfiguration);
            Guard.Against.Null(() => baseTypeConfiguration);

            return new EntityConfiguration
            {
                NaturalKeyPropertyName = typeConfiguration.NaturalKeyPropertyName ?? baseTypeConfiguration.NaturalKeyPropertyName,
                NaturalKeyStringEqualityComparer = typeConfiguration.NaturalKeyStringEqualityComparer ?? baseTypeConfiguration.NaturalKeyStringEqualityComparer,
                Mappings = typeConfiguration.Mappings ?? baseTypeConfiguration.Mappings, // TODO (Cameron): Not right.
            };
        }
    }
}
