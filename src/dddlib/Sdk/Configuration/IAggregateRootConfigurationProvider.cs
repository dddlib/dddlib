﻿// <copyright file="IAggregateRootConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using dddlib.Sdk;

    internal interface IAggregateRootConfigurationProvider
    {
        AggregateRootConfiguration GetConfiguration(Type type);
    }
}
