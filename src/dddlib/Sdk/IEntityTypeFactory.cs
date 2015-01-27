// <copyright file="IEntityTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    internal interface IEntityTypeFactory
    {
        EntityType Create(EntityConfiguration configuration);
    }
}
