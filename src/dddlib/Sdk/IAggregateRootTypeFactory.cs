// <copyright file="IAggregateRootTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    internal interface IAggregateRootTypeFactory
    {
        AggregateRootType Create(AggregateRootConfiguration configuration);
    }
}
