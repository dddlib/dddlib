// <copyright file="AssemblyConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Configuration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the assembly configuration.
    /// </summary>
    public class AssemblyConfiguration
    {
        private readonly Dictionary<Type, Func<object>> aggregateRootFactories = new Dictionary<Type, Func<object>>();

        //// private Func<Type, IEventDispatcher> eventDispatcherFactory;

        internal TypeConfiguration CreateConfiguration(Type type)
        {
            var aggregateRootFactory = default(Func<object>);
            if (this.aggregateRootFactories.TryGetValue(type, out aggregateRootFactory))
            {
                return new TypeConfiguration
                {
                    EventDispatcher = new DefaultEventDispatcher(type),
                    AggregateRootFactory = aggregateRootFactory,
                };
            }

            ////if (eventDispatcherFactory != null)
            ////{
            ////    return new TypeConfiguration(eventDispatcherFactory);
            ////}

            return new TypeConfiguration
            {
                EventDispatcher = new DefaultEventDispatcher(type),
            };
        }
    }
}
