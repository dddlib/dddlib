// <copyright file="Application.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using dddlib.Runtime.Configuration;

    /*  TODO (Cameron):
        Consider taking a Func<IConfiguration> as an argument to bypass the bootstrapper.
        Or an AssemblyConfiguration
        Or an instance of IBootstrapper - especially this because it allows for DI into the bootstrapper  */

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
        private readonly ITypeAnalyzer typeAnalyzer;

        private bool isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Application"/> class.
        /// </summary>
        public Application()
            : this(new DefaultTypeConfigurationProvider())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Application"/> class.
        /// </summary>
        /// <param name="typeConfigurationProvider">The type configuration provider.</param>
        public Application(ITypeConfigurationProvider typeConfigurationProvider)
            : this(typeConfigurationProvider, new TypeAnalyzer())
        {
        }

        internal Application(ITypeConfigurationProvider typeConfigurationProvider, ITypeAnalyzer typeAnalyzer)
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
                    typeConfiguration = this.typeConfigurationProvider.GetConfiguration(type);
                    typeDescriptor = this.typeAnalyzer.GetDescriptor(type, typeConfiguration);
                }
                catch (Exception ex)
                {
                    if (ex is RuntimeException)
                    {
                        throw;
                    }

                    var message = string.Format(
                        CultureInfo.InvariantCulture,
                        "The {0} of type '{1}' threw an exception during invocation.\r\nSee inner exception for details.",
                        typeConfiguration == null ? "type configuration provider" : "type analyzer",
                        typeConfiguration == null ? this.typeConfigurationProvider.GetType() : this.typeAnalyzer.GetType());

                    throw new RuntimeException(message, ex);
                }

                this.typeDescriptors.Add(type, typeDescriptor);

                return typeDescriptor;
            }
        }
    }
}
