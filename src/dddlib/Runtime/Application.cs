// <copyright file="Application.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    /*  TODO (Cameron): 
        Consider folding Runtime into Application.
        Apply 'is disposed' check when runtime is moved in.  */

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Represents an application.
    /// </summary>
    public sealed class Application : IDisposable
    {
        private static readonly Lazy<Application> DefaultApplication = new Lazy<Application>(() => new Application(), true);
        private static readonly List<Application> Applications = new List<Application>();
        private static readonly object SyncLock = new object();

        private readonly Dictionary<Type, TypeDescriptor> typeDescriptors = new Dictionary<Type, TypeDescriptor>();
        private readonly Func<Type, ITypeConfigurationProvider> configurationProviderFactory;

        private bool isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Application"/> class.
        /// </summary>
        public Application()
            : this(type => new DefaultTypeConfigurationProvider())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Application"/> class.
        /// </summary>
        /// <param name="configurationProviderFactory">The configuration provider factory.</param>
        public Application(Func<Type, ITypeConfigurationProvider> configurationProviderFactory)
        {
            Guard.Against.Null(() => configurationProviderFactory);

            this.configurationProviderFactory = configurationProviderFactory;

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

        internal TypeDescriptor GetAggregateRootDescriptor(Type type)
        {
            return this.GetDescriptor(type, typeof(AggregateRoot));
        }

        internal TypeDescriptor GetEntityDescriptor(Type type)
        {
            return this.GetDescriptor(type, typeof(Entity));
        }

        internal TypeDescriptor GetValueObjectDescriptor(Type type)
        {
            return this.GetDescriptor(type, typeof(ValueObject<>));
        }

        private TypeDescriptor GetDescriptor(Type type, Type descriptorType)
        {
            Guard.Against.Null(() => type);

            if (!descriptorType.IsAssignableFrom(type))
            {
                throw new ArgumentException("Invalid runtime type specified.", "type");
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

                var typeConfigurationProvider = default(ITypeConfigurationProvider);
                try
                {
                    typeConfigurationProvider = this.configurationProviderFactory(type);
                }
                catch (Exception ex)
                {
                    throw new RuntimeException(
                        "The type configuration provider factory threw an exception during invocation.\r\nSee inner exception for details.",
                        ex);
                }

                var typeConfiguration = default(TypeConfiguration);
                try
                {
                    typeConfiguration = typeConfigurationProvider.GetConfiguration(type);
                }
                catch (Exception ex)
                {
                    throw new RuntimeException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "The type configuration provider of type '{0}' threw an exception during invocation.\r\nSee inner exception for details.",
                            typeConfigurationProvider.GetType()),
                        ex);
                }

                this.typeDescriptors.Add(type, typeDescriptor = new TypeAnalyzer().GetDescriptor(type, typeConfiguration));

                return typeDescriptor;
            }
        }
    }
}
