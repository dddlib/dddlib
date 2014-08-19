// <copyright file="AggregateRootConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
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
        public Func<object> UninitializedFactory { get; set; }
    }
}
