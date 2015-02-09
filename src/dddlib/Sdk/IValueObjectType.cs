// <copyright file="IValueObjectType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    internal interface IValueObjectType
    {
        object EqualityComparer { get; }

        MapperCollection Mappings { get; }
    }
}
