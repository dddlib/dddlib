// <copyright file="TypeConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    /// <summary>
    /// Represents the type configuration.
    /// </summary>
    public sealed class TypeConfiguration
    {
        private readonly RuntimeMode runtimeMode;
        private readonly Func<Type, IEventDispatcher> eventDispatcherFactory;
        private readonly Func<object> aggregateRootFactory;

        private TypeConfiguration(RuntimeMode runtimeMode, Func<Type, IEventDispatcher> eventDispatcherFactory, Func<object> aggregateRootFactory)
        {
            this.runtimeMode = runtimeMode;
            this.eventDispatcherFactory = eventDispatcherFactory;
            this.aggregateRootFactory = aggregateRootFactory;
        }

        /// <summary>
        /// Gets the runtime mode.
        /// </summary>
        /// <value>The runtime mode.</value>
        public RuntimeMode RuntimeMode
        {
            get { return this.runtimeMode; }
        }

        /// <summary>
        /// Gets the event dispatcher factory.
        /// </summary>
        /// <value>The event dispatcher factory.</value>
        public Func<Type, IEventDispatcher> EventDispatcherFactory
        {
            get { return this.eventDispatcherFactory; }
        }

        /// <summary>
        /// Gets the aggregate root factory.
        /// </summary>
        /// <value>The aggregate root factory.</value>
        public Func<object> AggregateRootFactory
        {
            get { return this.aggregateRootFactory; }
        }

        /// <summary>
        /// Creates an event sourcing type configuration.
        /// </summary>
        /// <param name="eventDispatcherFactory">The event dispatcher factory.</param>
        /// <param name="aggregateRootFactory">The aggregate root factory.</param>
        /// <returns>An event sourcing type configuration.</returns>
        public static TypeConfiguration Create(Func<Type, IEventDispatcher> eventDispatcherFactory, Func<object> aggregateRootFactory)
        {
            return new TypeConfiguration(RuntimeMode.EventSourcing, eventDispatcherFactory, aggregateRootFactory);
        }

        /// <summary>
        /// Creates an event sourcing without persistence type configuration.
        /// </summary>
        /// <param name="eventDispatcherFactory">The event dispatcher factory.</param>
        /// <returns>An event sourcing without persistence type configuration.</returns>
        public static TypeConfiguration Create(Func<Type, IEventDispatcher> eventDispatcherFactory)
        {
            return new TypeConfiguration(RuntimeMode.EventSourcingWithoutPersistence, eventDispatcherFactory, null);
        }

        /// <summary>
        /// Creates a plain type configuration.
        /// </summary>
        /// <returns>A plain type configuration.</returns>
        public static TypeConfiguration Create()
        {
            return new TypeConfiguration(RuntimeMode.Plain, null, null);
        }
    }
}
