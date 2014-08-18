// <copyright file="AggregateRootConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class AggregateRootConfiguration
    {
        public Func<object> Factory { get; internal set; }

        public string ApplyMethodName { get; set; }
    }
}
