// <copyright file="ITypeConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using dddlib.Runtime.Configuration;

    /// <summary>
    /// Exposes the public members of the type configuration provider.
    /// </summary>
    public interface ITypeConfigurationProvider
    {
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <param name="type">The type to get the configuration for.</param>
        /// <returns>The configuration.</returns>
        TypeConfiguration GetConfiguration(Type type);
    }
}
