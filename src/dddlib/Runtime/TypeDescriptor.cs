// <copyright file="TypeDescriptor.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class TypeDescriptor
    {
        public IEventDispatcher EventDispatcher { get; internal set; }

        public IEqualityComparer<object> EqualityComparer { get; internal set; }

        public Func<object> Factory { get; internal set; }
    }
}
