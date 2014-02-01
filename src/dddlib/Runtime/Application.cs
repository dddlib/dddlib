// <copyright file="Application.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    internal sealed class Application
    {
        private static readonly Lazy<Application> Instance = new Lazy<Application>(() => new Application(), true);
        private static readonly object SyncLock = new object();

        private readonly Dictionary<Type, IEventDispatcher> dispatchers = new Dictionary<Type, IEventDispatcher>();
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

        // LINK (Cameron): http://stackoverflow.com/questions/1268397/how-to-find-all-the-types-in-an-assembly-that-inherit-from-a-specific-type-c-sha
        private static IEnumerable<Type> FindDerivedTypes(Assembly assembly, Type baseType)
        {
            return assembly.GetTypes().Where(t => t != baseType && baseType.IsAssignableFrom(t));
        }

        private void Bootstrap(Assembly assembly)
        {
            var bootstrappers = FindDerivedTypes(assembly, typeof(IBootstrapper));
        }
    }
}
