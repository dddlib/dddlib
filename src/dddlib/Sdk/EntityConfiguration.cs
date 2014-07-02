// <copyright file="EntityConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class EntityConfiguration
    {
        public string NaturalKeyPropertyName { get; set; }

        public Type EntityType { get; set; }

        public IEqualityComparer<string> NaturalKeyStringEqualityComparer { get; set; }
    }
}
