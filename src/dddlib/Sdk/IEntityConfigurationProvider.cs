// <copyright file="IEntityConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal interface IEntityConfigurationProvider
    {
        EntityConfiguration GetConfiguration(Type type);
    }
}
