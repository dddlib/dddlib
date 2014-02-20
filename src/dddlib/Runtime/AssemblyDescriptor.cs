// <copyright file="AssemblyDescriptor.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class AssemblyDescriptor
    {
        public IEventDispatcherFactory EventDispatcherFactory { get; internal set; }

        public IEnumerable<KeyValuePair<Type, Func<object>>> AggregateRootFactories { get; internal set; }
    }
}
