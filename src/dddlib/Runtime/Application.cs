// <copyright file="Application.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using dddlib.Sdk;

    /// <summary>
    /// Represents an application.
    /// </summary>
    public sealed class Application : IDisposable
    {
        private static readonly Lazy<Application> DefaultApplication = new Lazy<Application>(() => new Application(), true);
        private static readonly List<Application> Applications = new List<Application>();
        private static readonly object SyncLock = new object();

        private readonly Dictionary<Type, AggregateRootType> aggregateRootTypes = new Dictionary<Type, AggregateRootType>();
        private readonly Dictionary<Type, EntityType> entityTypes = new Dictionary<Type, EntityType>();
        private readonly Dictionary<Type, ValueObjectType> valueObjectTypes = new Dictionary<Type, ValueObjectType>();

        private readonly Func<Type, AggregateRootType> aggregateRootTypeFactory;
        private readonly Func<Type, EntityType> entityTypeFactory;
        private readonly Func<Type, ValueObjectType> valueObjectTypeFactory;

        private bool isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Application"/> class.
        /// </summary>
        public Application()
            : this(t => CreateAggregateRootType(t), t => CreateEntityType(t), t => CreateValueObjectType(t))
        {
        }

        internal Application(
            Func<Type, AggregateRootType> aggregateRootTypeFactory,
            Func<Type, EntityType> entityTypeFactory,
            Func<Type, ValueObjectType> valueObjectTypeFactory)
        {
            Guard.Against.Null(() => aggregateRootTypeFactory);
            Guard.Against.Null(() => entityTypeFactory);
            Guard.Against.Null(() => valueObjectTypeFactory);

            this.aggregateRootTypeFactory = aggregateRootTypeFactory;
            this.entityTypeFactory = entityTypeFactory;
            this.valueObjectTypeFactory = valueObjectTypeFactory;

            lock (SyncLock)
            {
                Applications.Add(this);
            }
        }

        /// <summary>
        /// Gets the ambient application instance.
        /// </summary>
        /// <value>The ambient application instance.</value>
        public static Application Current
        {
            get 
            {
                lock (SyncLock)
                {
                    // LINK (Cameron): http://stackoverflow.com/questions/1043039/does-listt-guarantee-insertion-order
                    return Applications.Any() ? Applications.Last() : DefaultApplication.Value;
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (object.ReferenceEquals(this, DefaultApplication.Value))
            {
                // NOTE (Cameron): We cannot allow the ambient application to be disposed.
                return;
            }

            lock (SyncLock)
            {
                if (this.isDisposed)
                {
                    return;
                }

                Applications.Remove(this);

                this.isDisposed = true;
            }
        }

        internal AggregateRootType GetAggregateRootType(Type type)
        {
            return this.GetType(type, this.aggregateRootTypes, this.aggregateRootTypeFactory);
        }

        internal EntityType GetEntityType(Type type)
        {
            return this.GetType(type, this.entityTypes, this.entityTypeFactory);
        }

        internal ValueObjectType GetValueObjectType(Type type)
        {
            return this.GetType(type, this.valueObjectTypes, this.valueObjectTypeFactory);
        }

        private static AggregateRootType CreateAggregateRootType(Type type)
        {
            var bootstrapper = new Bootstrapper();
            var typeAnalyzer = new AggregateRootAnalyzer();
            var manager = new AggregateRootConfigurationManager();
            var configProvider = new AggregateRootConfigurationProvider(bootstrapper, typeAnalyzer, manager);
            var configuration = configProvider.GetConfiguration(type);
            return new AggregateRootTypeFactory().Create(configuration);
        }

        private static EntityType CreateEntityType(Type type)
        {
            var bootstrapper = new Bootstrapper();
            var typeAnalyzer = new EntityAnalyzer();
            var manager = new EntityConfigurationManager();
            var configProvider = new EntityConfigurationProvider(bootstrapper, typeAnalyzer, manager);
            var configuration = configProvider.GetConfiguration(type);
            return new EntityTypeFactory().Create(configuration);
        }

        private static ValueObjectType CreateValueObjectType(Type type)
        {
            var bootstrapper = new Bootstrapper();
            var typeAnalyzer = new ValueObjectAnalyzer();
            var manager = new ValueObjectConfigurationManager();
            var configProvider = new ValueObjectConfigurationProvider(bootstrapper, typeAnalyzer, manager);
            var configuration = configProvider.GetConfiguration(type);
            return new ValueObjectTypeFactory().Create(configuration);
        }

        private T GetType<T>(Type type, IDictionary<Type, T> runtimeTypes, Func<Type, T> factory)
        {
            Guard.Against.Null(() => runtimeTypes);
            Guard.Against.Null(() => factory);

            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            var runtimeType = default(T);
            if (runtimeTypes.TryGetValue(type, out runtimeType))
            {
                return runtimeType;
            }

            lock (runtimeTypes)
            {
                if (runtimeTypes.TryGetValue(type, out runtimeType))
                {
                    return runtimeType;
                }

                try
                {
                    runtimeType = factory.Invoke(type);
                }
                catch (Exception ex)
                {
                    if (ex is RuntimeException)
                    {
                        throw;
                    }

                    throw new RuntimeException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "The type factory for type '{0}' threw an exception during invocation.\r\nSee inner exception for details.",
                            type),
                        ex);
                }

                runtimeTypes.Add(type, runtimeType);

                return runtimeType;
            }
        }
    }
}
