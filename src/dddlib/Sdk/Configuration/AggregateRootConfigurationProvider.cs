// <copyright file="AggregateRootConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Collections.Generic;

    internal class AggregateRootConfigurationProvider
    {
        private readonly Dictionary<Type, AggregateRootConfiguration> cachedConfiguration = new Dictionary<Type, AggregateRootConfiguration>();

        private readonly IBootstrapperProvider bootstrapperProvider;

        public AggregateRootConfigurationProvider(IBootstrapperProvider bootstrapperProvider)
        {
            Guard.Against.Null(() => bootstrapperProvider);

            this.bootstrapperProvider = bootstrapperProvider;
        }

        public AggregateRootConfiguration GetConfiguration(Type type)
        {
            // TODO (Cameron): This should be included in the configuration collection class then injected in.
            var runtimeTypeConfiguration = default(AggregateRootConfiguration);
            if (!this.cachedConfiguration.TryGetValue(type, out runtimeTypeConfiguration))
            {
                this.cachedConfiguration.Add(type, runtimeTypeConfiguration = this.GetRuntimeTypeConfiguration(type));
            }

            return runtimeTypeConfiguration;
        }

        private AggregateRootConfiguration GetRuntimeTypeConfiguration(Type type)
        {
            var bootstrapper = this.bootstrapperProvider.GetBootstrapper(type);

            // TODO (Cameron): This should be an injected configuration collection.
            var configuration = new BootstrapperConfiguration();

            bootstrapper.Invoke(configuration);

            var typeConfiguration = configuration.GetAggregateRootConfiguration(type);
            var baseTypeConfiguration = type.BaseType == typeof(AggregateRoot) ? new AggregateRootConfiguration() : this.GetConfiguration(type.BaseType);

            return AggregateRootConfiguration.Merge(typeConfiguration, baseTypeConfiguration);
        }
    }
}
