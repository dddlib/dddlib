// <copyright file="IAggregateRootTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    internal interface IAggregateRootTypeFactory
    {
        AggregateRootType Create(AggregateRootConfiguration configuration);
    }
}
