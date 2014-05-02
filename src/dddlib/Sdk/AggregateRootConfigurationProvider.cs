// <copyright file="AggregateRootConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class AggregateRootConfigurationProvider : IConfigurationProvider<AggregateRootConfiguration>
    {
        private readonly Dictionary<Type, AggregateRootConfiguration> config = new Dictionary<Type, AggregateRootConfiguration>();

        private readonly IConfigurationProvider<AggregateRootConfiguration> bootstrapper;
        private readonly IConfigurationProvider<AggregateRootConfiguration> typeAnalyzer;
        private readonly AggregateRootConfigurationManager manager;

        public AggregateRootConfigurationProvider(IConfigurationProvider<AggregateRootConfiguration> bootstrapper, IConfigurationProvider<AggregateRootConfiguration> typeAnalyzer, AggregateRootConfigurationManager manager)
        {
            this.bootstrapper = bootstrapper;
            this.typeAnalyzer = typeAnalyzer;
            this.manager = manager;
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

            return this.manager.Merge(typeConfiguration, baseTypeConfiguration);
        }

        private AggregateRootConfiguration GetTypeConfiguration(Type type)
        {
            var bootstrapperConfiguration = this.bootstrapper.GetConfiguration(type);
            var typeAnalyzerConfiguration = this.typeAnalyzer.GetConfiguration(type);

            return new[] { bootstrapperConfiguration, typeAnalyzerConfiguration }.Combine();
        }
    }
}
