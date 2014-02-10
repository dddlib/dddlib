// <copyright file="AssemblyDescriptor.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Analyzer
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Describes an assembly.
    /// </summary>
    public class AssemblyDescriptor : Descriptor
    {
        internal AssemblyDescriptor()
        {
        }

        /// <summary>
        /// Gets the event dispatcher factory.
        /// </summary>
        /// <value>The event dispatcher factory.</value>
        public Func<Type, IEventDispatcher> EventDispatcherFactory { get; internal set; }

        /// <summary>
        /// Gets the aggregate root factories.
        /// </summary>
        /// <value>The aggregate root factories.</value>
        public IEnumerable<KeyValuePair<Type, Func<object>>> AggregateRootFactories { get; internal set; }
    }
}
