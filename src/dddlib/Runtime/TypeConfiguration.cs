// <copyright file="TypeConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /*  TODO (Cameron): 
        Make method virtual.
        Change exceptions from RuntimeException exceptions.
        See note at bottom.
        Consider getting config from other sources eg. attributes? (Maybe not?)
        Need to validate configuration eg. cannot have an event dispatcher factory and run in Plain mode - decide all rules and where to validate.  */

    /// <summary>
    /// Represents the type configuration.
    /// </summary>
    public class TypeConfiguration : IConfiguration
    {
        private static readonly Func<Type, IEventDispatcher> DefaultEventDispatcherFactory = type => new DefaultEventDispatcher(type);

        private readonly Dictionary<Type, Func<object>> aggregateRootFactories = new Dictionary<Type, Func<object>>();

        private RuntimeMode runtimeMode;
        private Func<Type, IEventDispatcher> eventDispatcherFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeConfiguration"/> class.
        /// </summary>
        public TypeConfiguration()
        {
            // NOTE (Cameron): Default(s).
            this.runtimeMode = RuntimeMode.EventSourcing;
            this.eventDispatcherFactory = DefaultEventDispatcherFactory;
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
        /// Tries the get aggregate root factory.
        /// </summary>
        /// <param name="type">The type of aggregate root.</param>
        /// <param name="factory">The factory.</param>
        /// <returns>Returns <c>true</c> if the aggregate root factory has been returned; otherwise <c>false</c>.</returns>
        public bool TryGetAggregateRootFactory(Type type, out Func<object> factory)
        {
            return this.aggregateRootFactories.TryGetValue(type, out factory);
        }

        /// <summary>
        /// Sets the event dispatcher factory.
        /// </summary>
        /// <param name="factory">The factory.</param>
        public void SetEventDispatcherFactory(Func<Type, IEventDispatcher> factory)
        {
            Guard.Against.Null(() => factory);

            this.eventDispatcherFactory = factory;
        }

        /// <summary>
        /// Sets the runtime mode for the domain model contained within this assembly.
        /// </summary>
        /// <param name="mode">The runtime mode.</param>
        public void SetRuntimeMode(RuntimeMode mode)
        {
            if (mode == default(RuntimeMode))
            {
                // NOTE (Cameron): Unset enumeration.
                throw new ArgumentException("Value of enumeration cannot be unset.", "mode");
            }

            this.runtimeMode = mode;
        }

        /// <summary>
        /// Registers the specified factory for creating an uninitialized instance of an aggregate of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of aggregate root.</typeparam>
        /// <param name="factory">The factory for the aggregate root.</param>
        public void RegisterAggregateRootFactory<T>(Func<T> factory) where T : AggregateRoot
        {
            Guard.Against.Null(() => factory);

            if (this.aggregateRootFactories.ContainsKey(typeof(T)))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cannot register more than one aggregate root factory for the type '{0}'.",
                        typeof(T)));
            }

            // TODO (Cameron): Some expression voodoo to fix the double enclosing.
            this.aggregateRootFactories.Add(typeof(T), () => factory());
        }
    }
}
