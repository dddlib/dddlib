// <copyright file="AggregateRootConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Collections.Generic;
    using dddlib.Sdk;

    internal class AggregateRootConfigurationProvider : IAggregateRootConfigurationProvider
    {
        private readonly Dictionary<Type, AggregateRootConfiguration> config = new Dictionary<Type, AggregateRootConfiguration>();

        private readonly IAggregateRootConfigurationProvider bootstrapper;
        private readonly IAggregateRootConfigurationProvider typeAnalyzer;

        public AggregateRootConfigurationProvider(
            IAggregateRootConfigurationProvider bootstrapper, 
            IAggregateRootConfigurationProvider typeAnalyzer)
        {
            this.bootstrapper = bootstrapper;
            this.typeAnalyzer = typeAnalyzer;
        }

        public AggregateRootConfiguration GetConfiguration(Type type)
        {
            if (!typeof(AggregateRoot).IsAssignableFrom(type))
            {
                throw new Exception("not an aggregate!");
            }

            var runtimeTypeConfiguration = default(AggregateRootConfiguration);
            if (!this.config.TryGetValue(type, out runtimeTypeConfiguration))
            {
                this.config.Add(type, runtimeTypeConfiguration = this.GetRuntimeTypeConfiguration(type));
            }

            return runtimeTypeConfiguration;
        }

        private AggregateRootConfiguration GetRuntimeTypeConfiguration(Type type)
        {
            var typeConfiguration = this.GetTypeConfiguration(type);
            var baseTypeConfiguration = type.BaseType == typeof(AggregateRoot) ? new AggregateRootConfiguration() : this.GetConfiguration(type.BaseType);

            var config = AggregateRootConfiguration.Merge(typeConfiguration, baseTypeConfiguration);
            config.RuntimeType = type;

            return config;
        }

        private AggregateRootConfiguration GetTypeConfiguration(Type type)
        {
            var bootstrapperConfiguration = this.bootstrapper.GetConfiguration(type);
            var typeAnalyzerConfiguration = this.typeAnalyzer.GetConfiguration(type);

            return AggregateRootConfiguration.Combine(bootstrapperConfiguration, typeAnalyzerConfiguration);
        }
    }
}
