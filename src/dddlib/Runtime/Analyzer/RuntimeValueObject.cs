// <copyright file="RuntimeValueObject.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System.Collections.Generic;

    internal class RuntimeValueObject : RuntimeType
    {
        public object EqualityComparer { get; internal set; }
    }
}
