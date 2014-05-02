// <copyright file="IConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal interface IConfigurationProvider<T>
    {
        T GetConfiguration(Type type);
    }
}
