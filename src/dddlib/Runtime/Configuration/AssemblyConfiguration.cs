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
    public class AssemblyConfiguration : IConfiguration
    {
        private readonly Dictionary<Type, Func<object>> aggregateRootFactories = new Dictionary<Type, Func<object>>();

        //// private Func<Type, IEventDispatcher> eventDispatcherFactory;

        /// <summary>
        /// Gets the aggregate roots configuration options.
        /// </summary>
        /// <value>The aggregate roots configuration options.</value>
        public IConfigureAggregateRoots AggregateRoots
        {
            get { return new ConfigureAggregateRoots(this); }
        }

        /// <summary>
        /// Gets the aggregate root configuration options for the specified type of aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <returns>The aggregate root configuration options.</returns>
        public IConfigureAggregateRoot<T> AggregateRoot<T>() where T : AggregateRoot
        {
            return new ConfigureAggregateRoot<T>(this);
        }

        /// <summary>
        /// Gets the entity configuration options for the specified type of entity.
        /// </summary>
        /// <typeparam name="T">The type of entity.</typeparam>
        /// <returns>The entity configuration options.</returns>
        public IConfigureEntity<T> Entity<T>() where T : Entity
        {
            return new ConfigureEntity<T>(this);
        }

        internal TypeConfiguration CreateConfiguration(Type type)
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
