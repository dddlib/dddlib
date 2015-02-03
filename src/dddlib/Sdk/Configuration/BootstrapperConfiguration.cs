// <copyright file="BootstrapperConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Collections.Generic;
    using dddlib.Configuration;
    using dddlib.Runtime;
    using dddlib.Sdk;

    internal class BootstrapperConfiguration : IConfiguration
    {
        private Dictionary<Type, AggregateRootConfiguration> aggregateRootConfigurations = new Dictionary<Type, AggregateRootConfiguration>();
        private Dictionary<Type, EntityConfiguration> entityConfigurations = new Dictionary<Type, EntityConfiguration>();
        private Dictionary<Type, ValueObjectConfiguration> valueObjectConfigurations = new Dictionary<Type, ValueObjectConfiguration>();

        public IAggregateRootConfigurationWrapper<T> AggregateRoot<T>() where T : AggregateRoot
        {
            var configuration = default(AggregateRootConfiguration);
            if (!this.aggregateRootConfigurations.TryGetValue(typeof(T), out configuration))
            {
                this.aggregateRootConfigurations.Add(typeof(T), configuration = new AggregateRootConfiguration());
            }

            return new AggregateRootConfigurationWrapper<T>(configuration, this.Entity<T>());
        }

        public IEntityConfigurationWrapper<T> Entity<T>() where T : Entity
        {
            var configuration = default(EntityConfiguration);
            if (!this.entityConfigurations.TryGetValue(typeof(T), out configuration))
            {
                this.entityConfigurations.Add(typeof(T), configuration = new EntityConfiguration());
            }

            return new EntityConfigurationWrapper<T>(configuration);
        }

        public IValueObjectConfigurationWrapper<T> ValueObject<T>() where T : ValueObject<T>
        {
            var configuration = default(ValueObjectConfiguration);
            if (!this.valueObjectConfigurations.TryGetValue(typeof(T), out configuration))
            {
                this.valueObjectConfigurations.Add(typeof(T), configuration = new ValueObjectConfiguration());
            }

            return new ValueObjectConfigurationWrapper<T>(configuration);
        }

        // TODO (Cameron): Confirm type is valid.
        public AggregateRootConfiguration GetAggregateRootConfiguration(Type type)
        {
            this.aggregateRootConfigurations.TryGetValue(type, out var configuration);

            return configuration ?? new AggregateRootConfiguration();
        }

        public EntityConfiguration GetEntityConfiguration(Type type)
        {
            this.entityConfigurations.TryGetValue(type, out var configuration);

            return configuration ?? new EntityConfiguration();
        }

        public ValueObjectConfiguration GetValueObjectConfiguration(Type type)
        {
            this.valueObjectConfigurations.TryGetValue(type, out var configuration);

            return configuration ?? new ValueObjectConfiguration();
        }
    }
}
