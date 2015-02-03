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
        /// Gets or sets the runtime type of the value object.
        /// </summary>
        /// <value>The runtime type of the value object.</value>
        public Type RuntimeType { get; set; }

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
        /// Combines the specified primary and secondary value object configurations.
        /// </summary>
        /// <param name="primaryConfiguration">The primary configuration.</param>
        /// <param name="secondaryConfiguration">The secondary configuration.</param>
        /// <returns>The combined value object configuration.</returns>
        public static ValueObjectConfiguration Combine(ValueObjectConfiguration primaryConfiguration, ValueObjectConfiguration secondaryConfiguration)
        {
            Guard.Against.Null(() => primaryConfiguration);
            Guard.Against.Null(() => secondaryConfiguration);

            // TODO (Cameron): Confirm runtime types match?
            // TODO (Cameron): Ensure equality comparers match.
            return new ValueObjectConfiguration
            {
                RuntimeType = primaryConfiguration.RuntimeType ?? secondaryConfiguration.RuntimeType,
                EqualityComparer = primaryConfiguration.EqualityComparer ?? secondaryConfiguration.EqualityComparer,
                Mappings = primaryConfiguration.Mappings ?? secondaryConfiguration.Mappings, // TODO (Cameron): This is wrong.
            };
        }

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

            // TODO (Cameron): Confirm runtime types match?
            return new ValueObjectConfiguration
            {
                RuntimeType = typeConfiguration.RuntimeType,
                EqualityComparer = typeConfiguration.EqualityComparer,
                Mappings = typeConfiguration.Mappings, // TODO (Cameron): Is this wrong?
            };
        }
    }
}
