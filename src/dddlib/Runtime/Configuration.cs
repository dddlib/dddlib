// <copyright file="Configuration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Represents the configuration.
    /// </summary>
    public class Configuration : IConfiguration
    {
        private static readonly Func<Type, IEventDispatcher> DefaultEventDispatcherFactory = type => new DefaultEventDispatcher(type);

        private readonly Dictionary<Type, Func<object>> aggregateRootFactories = new Dictionary<Type, Func<object>>();

        private RuntimeMode runtimeMode;
        private Func<Type, IEventDispatcher> eventDispatcherFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="Configuration"/> class.
        /// </summary>
        public Configuration()
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

        void IConfiguration.SetEventDispatcherFactory(Func<Type, IEventDispatcher> factory)
        {
            Guard.Against.Null(() => factory);

            this.eventDispatcherFactory = factory;
        }

        void IConfiguration.SetRuntimeMode(RuntimeMode mode)
        {
            if (mode == default(RuntimeMode))
            {
                // NOTE (Cameron): Unset enumeration.
                throw new ArgumentException("Value of enumeration cannot be unset.", "mode");
            }

            this.runtimeMode = mode;
        }

        void IConfiguration.RegisterAggregateRootFactory<T>(Func<T> factory)
        {
            Guard.Against.Null(() => factory);

            if (this.aggregateRootFactories.ContainsKey(typeof(T)))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cannot register more than one aggregate root factory for the type '{0}'.",
                        typeof(T).Name));
            }

            // TODO (Cameron): Some expression voodoo to fix the double enclosing.
            this.aggregateRootFactories.Add(typeof(T), () => factory());
        }
    }
}
