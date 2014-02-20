// <copyright file="Configuration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    internal class Configuration : IConfiguration
    {
        private static readonly IEventDispatcherFactory DefaultEventDispatcherFactory = new DefaultEventDispatcherFactory();

        private readonly Dictionary<Type, Func<object>> aggregateRootFactories = new Dictionary<Type, Func<object>>();

        private RuntimeMode runtimeMode;
        private IEventDispatcherFactory eventDispatcherFactory;

        public Configuration()
        {
            // NOTE (Cameron): Default(s).
            this.runtimeMode = RuntimeMode.EventSourcing;
            this.eventDispatcherFactory = DefaultEventDispatcherFactory;
        }

        public RuntimeMode RuntimeMode
        {
            get { return this.runtimeMode; }
        }

        public IEventDispatcherFactory EventDispatcherFactory
        {
            get { return this.eventDispatcherFactory; }
        }

        public bool TryGetAggregateRootFactory(Type type, out Func<object> factory)
        {
            return this.aggregateRootFactories.TryGetValue(type, out factory);
        }

        public void SetEventDispatcherFactory(IEventDispatcherFactory factory)
        {
            Guard.Against.Null(() => factory);

            this.eventDispatcherFactory = factory;
        }

        public void SetRuntimeMode(RuntimeMode mode)
        {
            if (mode == default(RuntimeMode))
            {
                // NOTE (Cameron): Unset enumeration.
                throw new ArgumentException("Value of enumeration cannot be unset.", "mode");
            }

            this.runtimeMode = mode;
        }

        public void RegisterAggregateRootFactory<T>(Func<T> factory) where T : AggregateRoot
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
