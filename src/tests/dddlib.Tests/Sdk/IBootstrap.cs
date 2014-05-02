// <copyright file="IBootstrap.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Sdk
{
    using dddlib.Configuration;

    internal interface IBootstrap<T>
    {
        void Bootstrap(IConfiguration configure);
    }
}
