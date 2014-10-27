// <copyright file="IEntityTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    internal interface IEntityTypeFactory
    {
        EntityType Create(EntityConfiguration configuration);
    }
}
