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
        /// Bootstraps the specified domain.
        /// </summary>
        /// <param name="domain">The domain.</param>
        void Bootstrap(IDomain domain);
    }
}
