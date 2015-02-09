// <copyright file="IEntityType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System.Collections.Generic;

    internal interface IEntityType
    {
        NaturalKeySelector NaturalKeySelector { get; }

        IEqualityComparer<object> NaturalKeyEqualityComparer { get; }

        MapperCollection Mappings { get; }
    }
}
