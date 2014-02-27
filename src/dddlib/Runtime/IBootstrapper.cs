// <copyright file="IBootstrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    /// <summary>
    /// Exposes the public members of the bootstrapper.
    /// </summary>
    public interface IBootstrapper
    {
        /// <summary>
        /// Bootstraps the specified configuration.
        /// </summary>
        /// <param name="configure">The configuration.</param>
        void Bootstrap(IConfiguration configure);
    }
}
