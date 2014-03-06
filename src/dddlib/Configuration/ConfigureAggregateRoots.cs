// <copyright file="ConfigureAggregateRoots.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;

    internal class ConfigureAggregateRoots : IConfigureAggregateRoots
    {
        private readonly AssemblyConfiguration configuration;

        public ConfigureAggregateRoots(AssemblyConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            this.configuration = configuration;
        }

        public void ToNotDispatchEvents()
        {
            this.configuration.DoNotDispatchEvents();
        }
    }
}
