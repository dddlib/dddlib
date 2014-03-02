// <copyright file="TypeConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Configuration
{
    using System;

    /// <summary>
    /// Represents the type configuration.
    /// </summary>
    public sealed class TypeConfiguration
    {
        /// <summary>
        /// Gets or sets the event dispatcher.
        /// </summary>
        /// <value>The event dispatcher.</value>
        public IEventDispatcher EventDispatcher { get; set; }

        /// <summary>
        /// Gets or sets the aggregate root factory.
        /// </summary>
        /// <value>The aggregate root factory.</value>
        public Func<object> AggregateRootFactory { get; set; }
    }
}
