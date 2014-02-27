// <copyright file="ConfigureAggregateRoots.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Configuration
{
    using System;

    internal class ConfigureAggregateRoots : IConfigureAggregateRoots
    {
        public ConfigureAggregateRoots(AssemblyConfiguration configuration)
        {
        }

        public void ToNotDispatchEvents()
        {
        }

        public IConfigureAggregateRoots ToDispatchEventsUsing(Func<Type, IEventDispatcher> eventDispatcherFactory)
        {
            return this;
        }
    }
}
