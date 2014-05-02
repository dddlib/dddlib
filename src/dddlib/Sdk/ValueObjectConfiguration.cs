// <copyright file="ValueObjectConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal class ValueObjectConfiguration
    {
        public Func<object, object> Mapper { get; set; }
    }
}
