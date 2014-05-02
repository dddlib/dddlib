// <copyright file="EntityConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class EntityConfiguration
    {
        public Func<object, object> NaturalKeySelector { get; set; }

        public IEqualityComparer<string> NaturalKeyStringEqualityComparer { get; set; }
    }
}
