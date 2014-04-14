// <copyright file="Bootstrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Globalization;
    using System.Linq;
    using dddlib.Configuration;

    internal class Bootstrapper
    {
        public AggregateRootConfiguration GetAggregateRootConfiguration(Type type)
        {
            //// get the assembly for the type
            //// get the type of bootstrapper: GetBootstrapperInstance (for a given type) <- inject lookup?
            var bootstrapper = GetBootstrapper(type);

            //// create a config to run through the bootstrapper
            var assemblyConfiguration = new AssemblyConfiguration();
            var configuration = new BootstrapperConfiguration(assemblyConfiguration);

            //// get the config
            bootstrapper.Bootstrap(configuration);

            //// create a runtime config based on results
            var result = assemblyConfiguration.CreateConfiguration(type);

            var x = new DefaultTypeConfigurationProvider();
            var y = x.GetConfiguration(type);
            return new AggregateRootConfiguration
            {
                Factory = y.AggregateRootFactory,
                ApplyMethodName = null,
            };
        }

        public EntityConfiguration GetEntityConfiguration(Type type)
        {
            return new EntityConfiguration
            {
            };
        }

        private static IBootstrapper GetBootstrapper(Type type)
        {
            var bootstrapperTypes = type.Assembly.GetTypes().Where(assemblyType => typeof(IBootstrapper).IsAssignableFrom(assemblyType));
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
                        type.Assembly.GetName()));
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

        private class DefaultBootstrapper : IBootstrapper
        {
            public void Bootstrap(IConfiguration configure)
            {
            }
        }
    }
}
