// <copyright file="IValueObjectConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using dddlib.Sdk;

    internal interface IValueObjectConfigurationProvider
    {
        ValueObjectConfiguration GetConfiguration(Type type);
    }
}
