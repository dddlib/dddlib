// <copyright file="IConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    /*  TODO (Cameron): 
        Rename ITypeConfigurationProvider.  */

    /// <summary>
    /// Exposes the public members of the configuration provider.
    /// </summary>
    public interface IConfigurationProvider
    {
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <param name="type">The type to get the configuration for.</param>
        /// <returns>The configuration.</returns>
        Configuration GetConfiguration(Type type);
    }
}
