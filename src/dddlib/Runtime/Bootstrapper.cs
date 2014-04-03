// <copyright file="Bootstrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal class Bootstrapper
    {
        public AggregateRootConfiguration GetAggregateRootConfiguration(Type type)
        {
            //// get the assembly for the type
            //// get the type of bootstrapper: GetBootstrapperInstance (for a given type) <- inject lookup?
            //// create a config to run through the bootstrapper
            //// get the config
            //// create a runtime config based on results

            var x = new DefaultTypeConfigurationProvider();
            var y = x.GetConfiguration(type);
            return new AggregateRootConfiguration
            {
                Factory = y.AggregateRootFactory,
                ApplyMethodName = null,
            };
        }

        public EntityConfiguration GetEntityConfiguration(Type type)
        {
            return new EntityConfiguration
            {
            };
        }
    }
}
