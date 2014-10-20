// <copyright file="IValueObjectConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal interface IValueObjectConfigurationProvider
    {
        ValueObjectConfiguration GetConfiguration(Type type);
    }
}
