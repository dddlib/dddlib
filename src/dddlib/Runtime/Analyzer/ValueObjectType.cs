// <copyright file="ValueObjectType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System.Collections.Generic;

    internal class ValueObjectType : RuntimeType
    {
        public object EqualityComparer { get; internal set; }
    }
}
