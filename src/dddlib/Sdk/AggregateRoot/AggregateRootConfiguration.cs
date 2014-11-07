// <copyright file="AggregateRootConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using dddlib.Sdk;

    /// <summary>
    /// Represents the aggregate root configuration.
    /// </summary>
    public class AggregateRootConfiguration
    {
        /// <summary>
        /// Gets or sets the runtime type of the aggregate root.
        /// </summary>
        /// <value>The runtime type of the aggregate root.</value>
        public Type RuntimeType { get; set; }

        /// <summary>
        /// Gets or sets the uninitialized aggregate root factory.
        /// </summary>
        /// <value>The uninitialized aggregate root factory.</value>
        public Delegate UninitializedFactory { get; set; }
    }
}
