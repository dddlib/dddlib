// <copyright file="AggregateRootConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;

    /// <summary>
    /// Represents the aggregate root configuration.
    /// </summary>
    public class AggregateRootConfiguration
    {
        /// <summary>
        /// Gets or sets the uninitialized aggregate root factory.
        /// </summary>
        /// <value>The uninitialized aggregate root factory.</value>
        public Delegate UninitializedFactory { get; set; }

        /// <summary>
        /// Merges the specified type and base type aggregate root configurations.
        /// </summary>
        /// <param name="typeConfiguration">The type configuration.</param>
        /// <param name="baseTypeConfiguration">The base type configuration.</param>
        /// <returns>The merged aggregate root configuration.</returns>
        public static AggregateRootConfiguration Merge(AggregateRootConfiguration typeConfiguration, AggregateRootConfiguration baseTypeConfiguration)
        {
            Guard.Against.Null(() => typeConfiguration);
            Guard.Against.Null(() => baseTypeConfiguration);

            return new AggregateRootConfiguration
            {
                UninitializedFactory = typeConfiguration.UninitializedFactory,
            };
        }
    }
}
