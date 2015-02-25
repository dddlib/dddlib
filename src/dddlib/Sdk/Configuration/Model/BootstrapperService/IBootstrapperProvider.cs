// <copyright file="IBootstrapperProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Model.BootstrapperService
{
    using System;
    using dddlib.Configuration;

    /// <summary>
    /// Exposes the public members of the bootstrapper provider.
    /// </summary>
    public interface IBootstrapperProvider
    {
        /// <summary>
        /// Gets the bootstrapper for the specified type.
        /// </summary>
        /// <param name="type">The type to bootstrap.</param>
        /// <returns>The bootstrapper.</returns>
        Action<IConfiguration> GetBootstrapper(Type type);
    }
}
