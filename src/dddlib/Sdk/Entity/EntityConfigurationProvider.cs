// <copyright file="EntityConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class EntityConfigurationProvider : IEntityConfigurationProvider
    {
        private readonly Dictionary<Type, EntityConfiguration> config = new Dictionary<Type, EntityConfiguration>();

        private readonly IEntityConfigurationProvider bootstrapper;
        private readonly IEntityConfigurationProvider typeAnalyzer;
        private readonly EntityConfigurationManager manager;

        public EntityConfigurationProvider(
            IEntityConfigurationProvider bootstrapper, 
            IEntityConfigurationProvider typeAnalyzer, 
            EntityConfigurationManager manager)
        {
            this.bootstrapper = bootstrapper;
            this.typeAnalyzer = typeAnalyzer;
            this.manager = manager;
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

            var config = this.manager.Merge(typeConfiguration, baseTypeConfiguration);
            config.RuntimeType = type;

            return config;
        }

        private EntityConfiguration GetTypeConfiguration(Type type)
        {
            var bootstrapperConfiguration = this.bootstrapper.GetConfiguration(type);
            var typeAnalyzerConfiguration = this.typeAnalyzer.GetConfiguration(type);

            return new[] { bootstrapperConfiguration, typeAnalyzerConfiguration }.Combine();
        }
    }
}
