// <copyright file="Application.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    internal sealed class Application : IApplication
    {
        private static readonly Lazy<Application> Instance = new Lazy<Application>(() => new Application(), true);
        private static readonly object SyncLock = new object();

        private readonly Dictionary<Type, IEventDispatcher> dispatchers = new Dictionary<Type, IEventDispatcher>();
        private readonly Dictionary<Type, Func<object>> factories = new Dictionary<Type, Func<object>>();
        private readonly List<Assembly> assemblies = new List<Assembly>();

        internal Application()
        {
        }

        public static Application Current
        {
            get { return Instance.Value; }
        }

        public IEventDispatcher GetDispatcher(Type aggregateType)
        {
            var dispatcher = default(IEventDispatcher);
            if (!this.dispatchers.TryGetValue(aggregateType, out dispatcher))
            {
                lock (SyncLock)
                {
                    if (this.dispatchers.TryGetValue(aggregateType, out dispatcher))
                    {
                        return dispatcher;
                    }

                    if (!this.assemblies.Contains(aggregateType.Assembly))
                    {
                        this.Bootstrap(aggregateType.Assembly);
                        this.assemblies.Add(aggregateType.Assembly);
                    }

                    dispatcher = new EventDispatcher(aggregateType);
                    this.dispatchers.Add(aggregateType, dispatcher);
                }
            }

            return dispatcher;
        }

        void IApplication.RegisterFactory<T>(Func<T> aggregateFactory)
        {
            Guard.Against.Null(() => aggregateFactory);

            if (this.factories.ContainsKey(typeof(T)))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The application already has a factory registered for the aggregate root of type '{0}'.",
                        typeof(T).Name));
            }

            this.factories.Add(typeof(T), aggregateFactory);
        }

        private void Bootstrap(Assembly assembly)
        {
            var bootstrapperTypes = assembly.GetTypes().Where(type => typeof(IBootstrapper).IsAssignableFrom(type));
            if (!bootstrapperTypes.Any())
            {
                return;
            }

            if (bootstrapperTypes.Count() > 1)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The assembly '{0}' has more than one bootstrapper defined.",
                        this.GetType().Assembly.GetName().Name));
            }

            foreach (var bootstrapperType in bootstrapperTypes)
            {
                var bootstrapper = Activator.CreateInstance(bootstrapperType) as IBootstrapper;
                bootstrapper.Bootstrap(this);
            }
        }
    }
}
