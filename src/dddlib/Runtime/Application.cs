// <copyright file="Application.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Represents an application.
    /// </summary>
    public sealed class Application : IDisposable
    {
        private static readonly Type[] ValidTypes = new[] { typeof(AggregateRoot), typeof(Entity), typeof(ValueObject<>) };
        private static readonly Lazy<Application> DefaultApplication = new Lazy<Application>(() => new Application(), true);
        private static readonly List<Application> Applications = new List<Application>();
        private static readonly object SyncLock = new object();

        private readonly Dictionary<Type, AggregateRootType> aggregateRootTypes = new Dictionary<Type, AggregateRootType>();
        private readonly Dictionary<Type, EntityType> entityTypes = new Dictionary<Type, EntityType>();
        private readonly Dictionary<Type, ValueObjectType> valueObjectTypes = new Dictionary<Type, ValueObjectType>();

        private readonly ITypeFactory<AggregateRootType> aggregateRootTypeFactory;
        private readonly ITypeFactory<EntityType> entityTypeFactory;
        private readonly ITypeFactory<ValueObjectType> valueObjectTypeFactory;

        private bool isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Application"/> class.
        /// </summary>
        public Application()
            : this(CreateAggregateRootTypeFactory(), CreateEntityTypeFactory(), CreateValueObjectTypeFactory())
        {
        }

        internal Application(
            ITypeFactory<AggregateRootType> aggregateRootTypeFactory,
            ITypeFactory<EntityType> entityTypeFactory,
            ITypeFactory<ValueObjectType> valueObjectTypeFactory)
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
            if (!typeof(AggregateRoot).IsAssignableFrom(type))
            {
                throw new RuntimeException(string.Format(CultureInfo.InvariantCulture, "The specified type '{0}' is not an aggregate root.", type));
            }

            return this.GetType<AggregateRootType>(type, this.aggregateRootTypes, this.aggregateRootTypeFactory);
        }
        
        internal EntityType GetEntityType(Type type)
        {
            if (!typeof(Entity).IsAssignableFrom(type))
            {
                throw new RuntimeException(string.Format(CultureInfo.InvariantCulture, "The specified type '{0}' is not an entity.", type));
            }

            return this.GetType<EntityType>(type, this.entityTypes, this.entityTypeFactory);
        }

        internal ValueObjectType GetValueObjectType(Type type)
        {
            if (!IsSubclassOfRawGeneric(typeof(ValueObject<>), type))
            {
                throw new RuntimeException(string.Format(CultureInfo.InvariantCulture, "The specified type '{0}' is not a value object.", type));
            }

            return this.GetType<ValueObjectType>(type, this.valueObjectTypes, this.valueObjectTypeFactory);
        }

        // TODO (Cameron): Remove. Somehow.
        private static bool IsSubclassOfRawGeneric(Type generic, Type type)
        {
            while (type != null && type != typeof(object))
            {
                var cur = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (generic == cur)
                {
                    return true;
                }
            
                type = type.BaseType;
            }
         
            return false;
        }

        private static ITypeFactory<AggregateRootType> CreateAggregateRootTypeFactory()
        {
            var configurationProvider = new DefaultConfigurationProvider<AggregateRootConfiguration>(
                new IConfigurationProvider<AggregateRootConfiguration>[]
                {
                    new Bootstrapper(),
                    new AggregateRootAnalyzer(),
                },
                new AggregateRootConfigurationManager());

            return new AggregateRootTypeFactory(configurationProvider);
        }

        private static ITypeFactory<EntityType> CreateEntityTypeFactory()
        {
            var configurationProvider = new DefaultConfigurationProvider<EntityConfiguration>(
                new IConfigurationProvider<EntityConfiguration>[]
                {
                    new Bootstrapper(),
                    new EntityAnalyzer(),
                },
                new EntityConfigurationManager());

            return new EntityTypeFactory(configurationProvider);
        }

        private static ITypeFactory<ValueObjectType> CreateValueObjectTypeFactory()
        {
            var bootstrapper = new Bootstrapper();
            var typeAnalyzer = new ValueObjectAnalyzer();
            var manager = new ValueObjectConfigurationManager();
            var configProvider = new ValueObjectConfigurationProvider(bootstrapper, typeAnalyzer, manager);
            return new ValueObjectTypeFactory(configProvider);
        }

        private T GetType<T>(Type type, IDictionary<Type, T> runtimeTypes, ITypeFactory<T> factory)
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
                    runtimeType = factory.Create(type);
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
                            "The type factory of type '{0}' threw an exception during invocation.\r\nSee inner exception for details.",
                            factory.GetType()),
                        ex);
                }

                runtimeTypes.Add(type, runtimeType);

                return runtimeType;
            }
        }
    }
}
