// <copyright file="ValueObjectConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;

    /// <summary>
    /// Represents the value object configuration.
    /// </summary>
    public class ValueObjectConfiguration
    {
        /// <summary>
        /// Gets or sets the equality comparer.
        /// </summary>
        /// <value>The equality comparer.</value>
        //// TODO (Cameron): Make concrete type - not object.
        public object EqualityComparer { get; set; }

        /// <summary>
        /// Gets or sets the mappings for this value object.
        /// </summary>
        /// <value>The mappings for this value object.</value>
        public MapperCollection Mappings { get; set; }

        /// <summary>
        /// Merges the specified type and base type value object configurations.
        /// </summary>
        /// <param name="typeConfiguration">The type configuration.</param>
        /// <param name="baseTypeConfiguration">The base type configuration.</param>
        /// <returns>The merged value object configuration.</returns>
        public static ValueObjectConfiguration Merge(ValueObjectConfiguration typeConfiguration, ValueObjectConfiguration baseTypeConfiguration)
        {
            Guard.Against.Null(() => typeConfiguration);
            Guard.Against.Null(() => baseTypeConfiguration);

            return new ValueObjectConfiguration
            {
                EqualityComparer = typeConfiguration.EqualityComparer,
                Mappings = typeConfiguration.Mappings, // TODO (Cameron): Is this wrong?
            };
        }
    }
}
