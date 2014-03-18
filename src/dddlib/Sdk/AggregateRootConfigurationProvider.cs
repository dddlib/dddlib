// <copyright file="AggregateRootConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class AggregateRootConfigurationProvider
    {
        private readonly Dictionary<Type, AggregateRootConfiguration> config = new Dictionary<Type, AggregateRootConfiguration>();

        private readonly Bootstrapper bootstrapper;
        private readonly AggregateRootAnalyzer typeAnalyzer;
        private readonly AggregateRootConfigurationManager manager;

        public AggregateRootConfigurationProvider(Bootstrapper bootstrapper, AggregateRootAnalyzer typeAnalyzer, AggregateRootConfigurationManager manager)
        {
            this.bootstrapper = bootstrapper;
            this.typeAnalyzer = typeAnalyzer;
            this.manager = manager;
        }

        public AggregateRootConfiguration Get(Type type)
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
            var baseTypeConfiguration = type.BaseType == typeof(AggregateRoot) ? new AggregateRootConfiguration() : this.Get(type.BaseType);

            return this.manager.Merge(typeConfiguration, baseTypeConfiguration);
        }

        private AggregateRootConfiguration GetTypeConfiguration(Type type)
        {
            var bootstrapperConfiguration = this.bootstrapper.GetAggregateRootConfiguration(type);
            var typeAnalyzerConfiguration = this.typeAnalyzer.GetConfiguration(type);

            return new[] { bootstrapperConfiguration, typeAnalyzerConfiguration }.Combine();
        }
    }
}
