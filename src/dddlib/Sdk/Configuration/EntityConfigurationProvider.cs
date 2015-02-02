// <copyright file="EntityConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Collections.Generic;
    using dddlib.Sdk;

    internal class EntityConfigurationProvider : IEntityConfigurationProvider
    {
        private readonly Dictionary<Type, EntityConfiguration> config = new Dictionary<Type, EntityConfiguration>();

        private readonly IEntityConfigurationProvider bootstrapper;
        private readonly IEntityConfigurationProvider typeAnalyzer;

        public EntityConfigurationProvider(
            IEntityConfigurationProvider bootstrapper, 
            IEntityConfigurationProvider typeAnalyzer)
        {
            this.bootstrapper = bootstrapper;
            this.typeAnalyzer = typeAnalyzer;
        }

        public EntityConfiguration GetConfiguration(Type type)
        {
            if (!typeof(Entity).IsAssignableFrom(type))
            {
                throw new Exception("not an entity!");
            }

            var runtimeTypeConfiguration = default(EntityConfiguration);
            if (!this.config.TryGetValue(type, out runtimeTypeConfiguration))
            {
                this.config.Add(type, runtimeTypeConfiguration = this.GetRuntimeTypeConfiguration(type));
            }

            return runtimeTypeConfiguration;
        }

        private EntityConfiguration GetRuntimeTypeConfiguration(Type type)
        {
            var typeConfiguration = this.GetTypeConfiguration(type);
            var baseTypeConfiguration = type.BaseType == typeof(Entity) ? new EntityConfiguration() : this.GetConfiguration(type.BaseType);

            var config = EntityConfiguration.Merge(typeConfiguration, baseTypeConfiguration);
            config.RuntimeType = type;

            return config;
        }

        private EntityConfiguration GetTypeConfiguration(Type type)
        {
            var bootstrapperConfiguration = this.bootstrapper.GetConfiguration(type);
            var typeAnalyzerConfiguration = this.typeAnalyzer.GetConfiguration(type);

            return EntityConfiguration.Combine(bootstrapperConfiguration, typeAnalyzerConfiguration);
        }
    }
}
