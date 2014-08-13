// <copyright file="BootstrapperConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using dddlib.Runtime;

    internal class BootstrapperConfiguration : 
        IConfiguration,
        IConfigurationProvider<AggregateRootConfiguration>,
        IConfigurationProvider<EntityConfiguration>,
        IConfigurationProvider<ValueObjectConfiguration>
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

        AggregateRootConfiguration IConfigurationProvider<AggregateRootConfiguration>.GetConfiguration(Type type)
        {
            var configuration = default(AggregateRootConfiguration);
            this.aggregateRootConfigurations.TryGetValue(type, out configuration);

            return configuration ?? new AggregateRootConfiguration();
        }

        EntityConfiguration IConfigurationProvider<EntityConfiguration>.GetConfiguration(Type type)
        {
            var configuration = default(EntityConfiguration);
            this.entityConfigurations.TryGetValue(type, out configuration);

            return configuration ?? new EntityConfiguration();
        }

        ValueObjectConfiguration IConfigurationProvider<ValueObjectConfiguration>.GetConfiguration(Type type)
        {
            var configuration = default(ValueObjectConfiguration);
            this.valueObjectConfigurations.TryGetValue(type, out configuration);

            return configuration ?? new ValueObjectConfiguration();
        }
    }
}
