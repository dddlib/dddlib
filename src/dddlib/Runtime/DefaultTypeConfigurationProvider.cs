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

    /*  TODO (Cameron):
        Make method virtual.
        Consider removing RuntimeException - is this always invoked in a try catch that throws a runtime exception anyway?  */

    /// <summary>
    /// Represents the default type configuration provider.
    /// </summary>
    public class DefaultTypeConfigurationProvider : ITypeConfigurationProvider
    {
        private readonly Dictionary<Assembly, AssemblyConfiguration> assemblyConfigurations = new Dictionary<Assembly, AssemblyConfiguration>();
        private readonly Dictionary<Type, TypeConfiguration> typeConfigurations = new Dictionary<Type, TypeConfiguration>();

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

            this.typeConfigurations.Add(type, typeConfiguration = CreateConfiguration(type, assemblyConfiguration));

            return typeConfiguration;
        }

        [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204:StaticElementsMustAppearBeforeInstanceElements", Justification = "Not here.")]
        private AssemblyConfiguration GetConfiguration(Assembly assembly)
        {
            var assemblyConfiguration = default(AssemblyConfiguration);
            if (this.assemblyConfigurations.TryGetValue(assembly, out assemblyConfiguration))
            {
                return assemblyConfiguration;
            }

            this.assemblyConfigurations.Add(assembly, assemblyConfiguration = CreateConfiguration(assembly));

            return assemblyConfiguration;
        }

        private static TypeConfiguration CreateConfiguration(Type type, AssemblyConfiguration assemblyConfiguration)
        {
            switch (assemblyConfiguration.RuntimeMode)
            {
                case RuntimeMode.EventSourcing:
                    var aggregateRootFactory = default(Func<object>);
                    assemblyConfiguration.AggregateRootFactories.TryGetValue(type, out aggregateRootFactory);
                    return TypeConfiguration.Create(assemblyConfiguration.EventDispatcherFactory, aggregateRootFactory);

                case RuntimeMode.EventSourcingWithoutPersistence:
                    return TypeConfiguration.Create(assemblyConfiguration.EventDispatcherFactory);

                case RuntimeMode.Plain:
                default:
                    return TypeConfiguration.Create();
            }
        }

        private static AssemblyConfiguration CreateConfiguration(Assembly assembly)
        {
            var bootstrapperTypes = assembly.GetTypes().Where(assemblyType => typeof(IBootstrapper).IsAssignableFrom(assemblyType));
            if (!bootstrapperTypes.Any())
            {
                return new AssemblyConfiguration();
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

            var assemblyConfiguration = new AssemblyConfiguration();
            try
            {
                bootstrapper.Bootstrap(assemblyConfiguration);
            }
            catch (Exception ex)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The bootstrapper of type '{0}' threw an exception during invocation.\r\nSee inner exception for details.",
                        bootstrapperType),
                    ex);
            }

            return assemblyConfiguration;
        }
    }
}
