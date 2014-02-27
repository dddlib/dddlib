// <copyright file="AssemblyConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Configuration
{
    using System;
    using System.Collections.Generic;

    internal class AssemblyConfiguration : IConfiguration
    {
        private readonly Dictionary<Type, Func<object>> aggregateRootFactories = new Dictionary<Type, Func<object>>();

        //// private Func<Type, IEventDispatcher> eventDispatcherFactory;

        public IConfigureAggregateRoots AggregateRoots
        {
            get { return new ConfigureAggregateRoots(this); }
        }

        public IConfigureAggregateRoot<T> AggregateRoot<T>() where T : AggregateRoot
        {
            return new ConfigureAggregateRoot<T>(this);
        }

        ////public IConfigureEntity<T> Entity<T>() where T : Entity
        ////{
        ////    return new ConfigureEntity<T>(this);
        ////}

        public TypeConfiguration CreateConfiguration(Type type)
        {
            var aggregateRootFactory = default(Func<object>);
            if (this.aggregateRootFactories.TryGetValue(type, out aggregateRootFactory))
            {
                return new TypeConfiguration(new DefaultEventDispatcher(type), aggregateRootFactory);
            }

            ////if (eventDispatcherFactory != null)
            ////{
            ////    return new TypeConfiguration(eventDispatcherFactory);
            ////}

            return new TypeConfiguration(new DefaultEventDispatcher(type));
        }
    }
}
