// <copyright file="DefaultConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class DefaultConfigurationProvider<T> where T : new()
    {
        private readonly List<Func<Type, T>> configurationProviders = new List<Func<Type, T>>();
        private readonly Dictionary<Type, T> configurations = new Dictionary<Type, T>();

        private readonly IConfigurationManager<T> configurationMerger;

        public DefaultConfigurationProvider(
            IEnumerable<Func<Type, T>> configurationProviders,
            IConfigurationManager<T> configurationMerger)
        {
            Guard.Against.Null(() => configurationMerger);

            this.configurationProviders.AddRange(configurationProviders);
            this.configurationMerger = configurationMerger;
        }

        public T GetConfiguration(Type type)
        {
            var configuration = default(T);
            if (!this.configurations.TryGetValue(type, out configuration))
            {
                this.configurations.Add(type, configuration = this.GetRuntimeTypeConfiguration(type));
            }

            return configuration;
        }

        private T GetRuntimeTypeConfiguration(Type type)
        {
            // TODO (Cameron): Remove dynamic and Microsoft.CSharp reference.
            dynamic allConfigurations = this.configurationProviders.Select(configurationProvider => configurationProvider(type));
            var typeConfiguration = ConfigurationExtensions.Combine(allConfigurations);

            var baseTypeConfiguration =
                typeof(AggregateRoot).Assembly.GetTypes().Contains(type.BaseType.IsGenericType ? type.BaseType.GetGenericTypeDefinition() : type.BaseType)
                ? new T() 
                : this.GetConfiguration(type.BaseType);

            return this.configurationMerger.Merge(typeConfiguration, baseTypeConfiguration);
        }
    }
}
