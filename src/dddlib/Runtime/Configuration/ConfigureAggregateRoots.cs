// <copyright file="ConfigureAggregateRoots.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Configuration
{
    using System;

    internal class ConfigureAggregateRoots : IConfigureAggregateRoots
    {
        private readonly RuntimeConfiguration configuration;

        public ConfigureAggregateRoots(RuntimeConfiguration configuration)
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
