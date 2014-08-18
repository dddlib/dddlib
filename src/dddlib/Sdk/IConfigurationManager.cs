// <copyright file="IConfigurationManager.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    internal interface IConfigurationManager<T>
    {
        T Merge(T configuration, T baseConfiguration);
    }
}
