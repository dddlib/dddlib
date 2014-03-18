// <copyright file="EntityConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class EntityConfigurationProvider
    {
        private readonly Dictionary<Type, EntityConfiguration> config = new Dictionary<Type, EntityConfiguration>();

        private readonly Bootstrapper bootstrapper;
        private readonly EntityAnalyzer typeAnalyzer;
        private readonly EntityConfigurationManager manager;

        public EntityConfigurationProvider(Bootstrapper bootstrapper, EntityAnalyzer typeAnalyzer, EntityConfigurationManager manager)
        {
            this.bootstrapper = bootstrapper;
            this.typeAnalyzer = typeAnalyzer;
            this.manager = manager;
        }

        public EntityConfiguration Get(Type type)
        {
            if (!typeof(AggregateRoot).IsAssignableFrom(type))
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
            var baseTypeConfiguration = type.BaseType == typeof(Entity) ? new EntityConfiguration() : this.Get(type.BaseType);

            return this.manager.Merge(typeConfiguration, baseTypeConfiguration);
        }

        private EntityConfiguration GetTypeConfiguration(Type type)
        {
            var bootstrapperConfiguration = this.bootstrapper.GetEntityConfiguration(type);
            var typeAnalyzerConfiguration = this.typeAnalyzer.GetConfiguration(type);

            return new[] { bootstrapperConfiguration, typeAnalyzerConfiguration }.Combine();
        }
    }
}
