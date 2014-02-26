// <copyright file="AssemblyConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
using System.Reflection;

    internal class AssemblyConfiguration : IConfiguration
    {
        /*  TODO (Cameron): 
            Change exceptions from RuntimeException exceptions... MAYBE NOT?
            See note at bottom.
            Need to validate configuration eg. cannot have an event dispatcher factory and run in Plain mode - decide all rules and where to validate.
            Consider Guard extension for Enum: Guard.Against.InvalidEnum(() => enum);
            Consider an undefined runtime mode, or a nullable property getter.  */

        private static readonly Func<Type, IEventDispatcher> DefaultEventDispatcherFactory = type => new DefaultEventDispatcher(type);

        private readonly Dictionary<Type, Func<object>> aggregateRootFactories = new Dictionary<Type, Func<object>>();

        private RuntimeMode runtimeMode;
        private Func<Type, IEventDispatcher> eventDispatcherFactory;

        public AssemblyConfiguration()
        {
            // default(s)
            this.runtimeMode = RuntimeMode.EventSourcing;
            this.eventDispatcherFactory = DefaultEventDispatcherFactory;
        }

        public RuntimeMode RuntimeMode
        {
            get { return this.runtimeMode; }
        }

        public Func<Type, IEventDispatcher> EventDispatcherFactory
        {
            get { return this.eventDispatcherFactory; }
        }

        public IReadOnlyDictionary<Type, Func<object>> AggregateRootFactories
        {
            get { return this.aggregateRootFactories; }
        }

        public void SetEventDispatcherFactory(Func<Type, IEventDispatcher> factory)
        {
            Guard.Against.Null(() => factory);

            this.eventDispatcherFactory = factory;
        }

        // LINK (Cameron): http://blogs.msdn.com/b/brada/archive/2003/11/29/50903.aspx
        public void SetRuntimeMode(RuntimeMode mode)
        {
            if (!Enum.GetValues(typeof(RuntimeMode)).Cast<RuntimeMode>().Contains(mode))
            {
                // NOTE (Cameron): Unset enumeration.
                throw new ArgumentException("Enumeration value is invalid.", "mode");
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
                        typeof(T)));
            }

            // TODO (Cameron): Some expression voodoo to fix the double enclosing.
            this.aggregateRootFactories.Add(typeof(T), () => factory());
        }
    }
}
