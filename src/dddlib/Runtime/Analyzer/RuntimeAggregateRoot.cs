// <copyright file="RuntimeAggregateRoot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class RuntimeAggregateRoot : RuntimeType
    {
        public Func<object> Factory { get; internal set; }

        public IEqualityComparer<object> EqualityComparer { get; internal set; }

        public IEventDispatcher EventDispatcher { get; internal set; }

        public RuntimeOptions Options { get; internal set; }

        public class RuntimeOptions
        {
            public bool PersistEvents { get; internal set; }

            public bool DispatchEvents { get; internal set; }
        }
    }
}
