// <copyright file="ITypeConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal interface ITypeConfigurationProvider
    {
        TypeConfiguration GetConfiguration(Type type);
    }
}
