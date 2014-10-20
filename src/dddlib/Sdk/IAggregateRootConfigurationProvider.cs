// <copyright file="IAggregateRootConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal interface IAggregateRootConfigurationProvider
    {
        AggregateRootConfiguration GetConfiguration(Type type);
    }
}
