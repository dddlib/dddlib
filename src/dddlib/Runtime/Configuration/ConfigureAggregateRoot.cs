// <copyright file="ConfigureAggregateRoot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Configuration
{
    using System;

    internal class ConfigureAggregateRoot<T> : IConfigureAggregateRoot<T> 
        where T : AggregateRoot
    {
        public ConfigureAggregateRoot(AssemblyConfiguration configuration)
        {
        }

        public IConfigureAggregateRoot<T> ToReconstituteUsing(Func<T> uninitializedFactory)
        {
            return this;
        }

        public IConfigureAggregateRoot<T> ToUseNaturalKey(Func<T, object> naturalKeySelector)
        {
            return this;
        }

        public void ToNotDispatchEvents()
        {
        }

        public IConfigureAggregateRoot<T> ToDispatchEventsUsing(Func<Type, IEventDispatcher> eventDispatcherFactory)
        {
            return this;
        }
    }
}
