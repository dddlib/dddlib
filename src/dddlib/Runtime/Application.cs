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

        private readonly Dictionary<Type, TypeDescriptor> typeDescriptors = new Dictionary<Type, TypeDescriptor>();

        private readonly ITypeConfigurationProvider typeConfigurationProvider;
        private readonly ITypeAnalyzer2 typeAnalyzer;

        private bool isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Application"/> class.
        /// </summary>
        public Application()
            : this(new DefaultTypeConfigurationProvider(), new TypeAnalyzer())
        {
        }

        internal Application(ITypeConfigurationProvider typeConfigurationProvider, ITypeAnalyzer2 typeAnalyzer)
        {
            Guard.Against.Null(() => typeConfigurationProvider);
            Guard.Against.Null(() => typeAnalyzer);

            this.typeConfigurationProvider = typeConfigurationProvider;
            this.typeAnalyzer = typeAnalyzer;

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

        internal T Get<T>(Type type) where T : class // IDomainType
        {
            if (typeof(T) == typeof(AggregateRootType))
            {
                return this.GetAggregateRootType(type) as T;
            }

            if (typeof(T) == typeof(EntityType))
            {
                return this.GetEntityType(type) as T;
            }

            if (typeof(T) == typeof(ValueObjectType))
            {
                return this.GetValueObjectType(type) as T;
            }

            throw new NotSupportedException();
        }

        internal AggregateRootType GetAggregateRootType(Type type)
        {
            var bootstrapper = new Bootstrapper();          // .GetConfig(type) for IBootstrapper
            var typeAnalyzer = new AggregateRootAnalyzer(); // .GetConfig(type) for type
            var manager = new AggregateRootConfigurationManager();

            /*  NOTE (Cameron): The application (this class) should not need to care about where the configuration comes from.
             *  Basically, this needs to be a minor re-write of what is below for each of the type (Aggregate, Entity, etc.) overloads
             *  passing in a function delegate for the config provider call (or something like that) */
            var configProvider = new AggregateRootConfigurationProvider(bootstrapper, typeAnalyzer, manager);
            var factory = new AggregateRootTypeFactory(configProvider);

            return factory.Create(type);
        }

        internal EntityType GetEntityType(Type type)
        {
            var bootstrapper = new Bootstrapper();
            var typeAnalyzer = new EntityAnalyzer();
            var manager = new EntityConfigurationManager();
            var configProvider = new EntityConfigurationProvider(bootstrapper, typeAnalyzer, manager);
            var factory = new EntityTypeFactory(configProvider);

            return factory.Create(type);
        }

        internal ValueObjectType GetValueObjectType(Type type)
        {
            var bootstrapper = new Bootstrapper();
            var typeAnalyzer = new ValueObjectAnalyzer();
            var manager = new ValueObjectConfigurationManager();
            var configProvider = new ValueObjectConfigurationProvider(bootstrapper, typeAnalyzer, manager);
            var factory = new ValueObjectTypeFactory(configProvider);

            return factory.Create(type);
        }

        internal TypeDescriptor GetTypeDescriptor(Type type)
        {
            Guard.Against.Null(() => type);

            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            if (!ValidTypes.Any(baseType => baseType.IsAssignableFrom(type)))
            {
                throw new RuntimeException(
                    string.Format(CultureInfo.InvariantCulture, "The specified type '{0}' is not a valid runtime type.", type));
            }

            var typeDescriptor = default(TypeDescriptor);
            if (this.typeDescriptors.TryGetValue(type, out typeDescriptor))
            {
                return typeDescriptor;
            }

            lock (this.typeDescriptors)
            {
                if (this.typeDescriptors.TryGetValue(type, out typeDescriptor))
                {
                    return typeDescriptor;
                }

                var typeConfiguration = default(TypeConfiguration);
                try
                {
                    // TODO (Cameron): This should be the config provider call (for each domain type).
                    typeConfiguration = this.typeConfigurationProvider.GetConfiguration(type);
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
                            "The type configuration provider of type '{0}' threw an exception during invocation.\r\nSee inner exception for details.",
                            this.typeConfigurationProvider.GetType()), 
                        ex);
                }

                try
                {
                    // TODO (Cameron): This should be the factory call (for each domain config type).
                    typeDescriptor = this.typeAnalyzer.GetDescriptor(type, typeConfiguration);
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
                            "The type analyzer of type '{0}' threw an exception during invocation.\r\nSee inner exception for details.",
                            this.typeAnalyzer.GetType()),
                        ex);
                }

                this.typeDescriptors.Add(type, typeDescriptor);

                return typeDescriptor;
            }
        }
    }
}
