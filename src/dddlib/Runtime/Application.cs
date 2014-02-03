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
        private readonly Dictionary<Type, IEqualityComparer<object>> equalityComparers = new Dictionary<Type, IEqualityComparer<object>>();
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

        public IEqualityComparer<object> GetEqualityComparer(Type entityType)
        {
            var equalityComparer = default(IEqualityComparer<object>);
            if (!this.equalityComparers.TryGetValue(entityType, out equalityComparer))
            {
                lock (SyncLock)
                {
                    if (this.equalityComparers.TryGetValue(entityType, out equalityComparer))
                    {
                        return equalityComparer;
                    }

                    if (!this.assemblies.Contains(entityType.Assembly))
                    {
                        this.Bootstrap(entityType.Assembly);
                        this.assemblies.Add(entityType.Assembly);
                    }

                    var naturalKey = entityType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                        .SelectMany(member => member.GetCustomAttributes(typeof(NaturalKeyAttribute), false))
                        .OfType<NaturalKeyAttribute>()
                        .SingleOrDefault();

                    if (naturalKey == null)
                    {
                        ////throw new RuntimeException(
                        ////    string.Format(
                        ////        CultureInfo.InvariantCulture,
                        ////        "The entity of type '{0}' does not have a natural key defined.",
                        ////        entityType.Name));
                        return EqualityComparer<object>.Default;
                    }

                    // TODO (Cameron): Ensure equality comparer instantiation is safe. ie. error-handled correctly.
                    equalityComparer = naturalKey.EqualityComparer == null
                        ? EqualityComparer<object>.Default
                        : (IEqualityComparer<object>)Activator.CreateInstance(naturalKey.EqualityComparer);

                    this.equalityComparers.Add(entityType, equalityComparer);
                }
            }

            return equalityComparer;
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
