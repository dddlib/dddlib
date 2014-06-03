// <copyright file="ValueObjectType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System.Collections.Generic;

    internal class ValueObjectType // where T : ValueObject<T>
    {
        public IEqualityComparer<object> EqualityComparer { get; internal set; }
        ////public IEqualityComparer<ValueObject<T>> EqualityComparer { get; internal set; }
    }
}
