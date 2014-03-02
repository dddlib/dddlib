// <copyright file="DefaultTypeConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using dddlib.Runtime.Configuration;

    /*  TODO (Cameron):
        Make method virtual.
        Consider getting config from other sources eg. attributes? (Maybe not?)
        Consider removing RuntimeException - is this always invoked in a try catch that throws a runtime exception anyway?  */

    /// <summary>
    /// Represents the default type configuration provider.
    /// </summary>
    public class DefaultTypeConfigurationProvider : ITypeConfigurationProvider
    {
        private readonly Dictionary<Assembly, RuntimeConfiguration> assemblyConfigurations = new Dictionary<Assembly, RuntimeConfiguration>();
        private readonly Dictionary<Type, TypeConfiguration> typeConfigurations = new Dictionary<Type, TypeConfiguration>();

        private readonly IBootstrapper bootstrapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultTypeConfigurationProvider"/> class.
        /// </summary>
        /// <param name="bootstrapper">The bootstrapper.</param>
        public DefaultTypeConfigurationProvider(IBootstrapper bootstrapper)
        {
            this.bootstrapper = bootstrapper;
        }

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <param name="type">The type to get the configuration for.</param>
        /// <returns>The configuration.</returns>
        public TypeConfiguration GetConfiguration(Type type)
        {
            Guard.Against.Null(() => type);

            var typeConfiguration = default(TypeConfiguration);
            if (this.typeConfigurations.TryGetValue(type, out typeConfiguration))
            {
                return typeConfiguration;
            }

            var assemblyConfiguration = this.GetConfiguration(type.Assembly);

            this.typeConfigurations.Add(type, typeConfiguration = assemblyConfiguration.CreateConfiguration(type));

            return typeConfiguration;
        }

        [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204:StaticElementsMustAppearBeforeInstanceElements", Justification = "Not here.")]
        private RuntimeConfiguration GetConfiguration(Assembly assembly)
        {
            var assemblyConfiguration = default(RuntimeConfiguration);
            if (this.assemblyConfigurations.TryGetValue(assembly, out assemblyConfiguration))
            {
                return assemblyConfiguration;
            }

            var bootstrapper = this.bootstrapper ?? GetAssemblyBootstrapper(assembly);

            this.assemblyConfigurations.Add(assembly, assemblyConfiguration = CreateConfiguration(bootstrapper));

            return assemblyConfiguration;
        }

        private static IBootstrapper GetAssemblyBootstrapper(Assembly assembly)
        {
            var bootstrapperTypes = assembly.GetTypes().Where(assemblyType => typeof(IBootstrapper).IsAssignableFrom(assemblyType));
            if (!bootstrapperTypes.Any())
            {
                return new DefaultBootstrapper();
            }

            if (bootstrapperTypes.Count() > 1)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture, 
                        "The assembly '{0}' has more than one bootstrapper defined.",
                        assembly.GetName()));
            }

            var bootstrapperType = bootstrapperTypes.First();
            if (bootstrapperType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The bootstrapper of type '{0}' cannot be instantiated as it does not have a default constructor.",
                        bootstrapperType));
            }

            var bootstrapper = default(IBootstrapper);
            try
            {
                bootstrapper = (IBootstrapper)Activator.CreateInstance(bootstrapperType);
            }
            catch (Exception ex)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The bootstrapper of type '{0}' threw an exception during instantiation.\r\nSee inner exception for details.",
                        bootstrapperType),
                    ex);
            }

            return bootstrapper;
        }

        private static RuntimeConfiguration CreateConfiguration(IBootstrapper bootstrapper)
        {
            var assemblyConfiguration = new RuntimeConfiguration();
            var configuration = new BootstrapperConfiguration(assemblyConfiguration);
            try
            {
                bootstrapper.Bootstrap(configuration);
            }
            catch (Exception ex)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The bootstrapper of type '{0}' threw an exception during invocation.\r\nSee inner exception for details.",
                        bootstrapper.GetType()),
                    ex);
            }

            return configuration.AssemblyConfiguration;
        }

        private class DefaultBootstrapper : IBootstrapper
        {
            public void Bootstrap(IConfiguration configure)
            {
            }
        }
    }
}
