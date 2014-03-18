// <copyright file="EntityType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class EntityType
    {
        public Func<object, object> NaturalKeySelector { get; internal set; }

        public IEqualityComparer<object> NaturalKeyEqualityComparer { get; internal set; }
    }
}
