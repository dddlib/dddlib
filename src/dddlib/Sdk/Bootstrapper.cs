// <copyright file="Bootstrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Globalization;
    using System.Linq;
    using dddlib.Configuration;

    internal class Bootstrapper :
        IAggregateRootConfigurationProvider,
        IEntityConfigurationProvider,
        IValueObjectConfigurationProvider
    {
        private readonly Func<Type, Action<IConfiguration>> bootstrapperProvider;
        private readonly Mapper mapper;

        public Bootstrapper(Mapper mapper)
            : this(GetBootstrapper, mapper)
        {
        }

        public Bootstrapper(Func<Type, Action<IConfiguration>> getBootstrapper, Mapper mapper)
        {
            Guard.Against.Null(() => getBootstrapper);
            Guard.Against.Null(() => mapper);

            this.bootstrapperProvider = getBootstrapper;
            this.mapper = mapper;
        }

        AggregateRootConfiguration IAggregateRootConfigurationProvider.GetConfiguration(Type type)
        {
            var bootstrap = this.bootstrapperProvider(type);

            // create a config to run through the bootstrapper
            var configuration = new BootstrapperConfiguration(this.mapper);

            // bootstrap
            bootstrap(configuration);

            return ((IAggregateRootConfigurationProvider)configuration).GetConfiguration(type);
        }

        EntityConfiguration IEntityConfigurationProvider.GetConfiguration(Type type)
        {
            var bootstrap = this.bootstrapperProvider(type);

            // create a config to run through the bootstrapper
            var configuration = new BootstrapperConfiguration(this.mapper);

            // bootstrap
            bootstrap(configuration);

            return ((IEntityConfigurationProvider)configuration).GetConfiguration(type);
        }

        ValueObjectConfiguration IValueObjectConfigurationProvider.GetConfiguration(Type type)
        {
            var bootstrap = this.bootstrapperProvider(type);

            // create a config to run through the bootstrapper
            var configuration = new BootstrapperConfiguration(this.mapper);

            // bootstrap
            bootstrap(configuration);

            return ((IValueObjectConfigurationProvider)configuration).GetConfiguration(type);
        }

        // TODO (Cameron): Consider BootstrapperProvider class.
        private static Action<IConfiguration> GetBootstrapper(Type type)
        {
            var bootstrapperTypes = type.Assembly.GetTypes()
                .Where(assemblyType => typeof(IBootstrapper).IsAssignableFrom(assemblyType) && assemblyType != typeof(IBootstrapper));

            if (!bootstrapperTypes.Any())
            {
                return config => { };
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

            return bootstrapper.Bootstrap;
        }
    }
}
