// <copyright file="Bootstrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    using dddlib.Runtime;

    internal class Bootstrapper : IBootstrapper
    {
        public void Bootstrap(IApplication application)
        {
        }

        public void Bootstrap(IDomain domain)
        {
            domain.RegisterUninitializedAggregateRootFactory(() => new Car());
        }
    }
}
