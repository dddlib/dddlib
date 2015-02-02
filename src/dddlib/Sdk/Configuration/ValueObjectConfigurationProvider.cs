// <copyright file="ValueObjectConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Collections.Generic;
    using dddlib.Sdk;

    internal class ValueObjectConfigurationProvider : IValueObjectConfigurationProvider
    {
        private readonly Dictionary<Type, ValueObjectConfiguration> config = new Dictionary<Type, ValueObjectConfiguration>();

        private readonly IValueObjectConfigurationProvider bootstrapper;
        private readonly IValueObjectConfigurationProvider typeAnalyzer;

        public ValueObjectConfigurationProvider(
            IValueObjectConfigurationProvider bootstrapper, 
            IValueObjectConfigurationProvider typeAnalyzer)
        {
            this.bootstrapper = bootstrapper;
            this.typeAnalyzer = typeAnalyzer;
        }

        public ValueObjectConfiguration GetConfiguration(Type type)
        {
            var runtimeTypeConfiguration = default(ValueObjectConfiguration);
            if (!this.config.TryGetValue(type, out runtimeTypeConfiguration))
            {
                this.config.Add(type, runtimeTypeConfiguration = this.GetRuntimeTypeConfiguration(type));
            }

            return runtimeTypeConfiguration;
        }

        // TODO (Cameron): Remove. Somehow.
        private static bool IsSubclassOfRawGeneric(Type generic, Type type)
        {
            while (type != null && type != typeof(object))
            {
                var cur = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (generic == cur)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private ValueObjectConfiguration GetRuntimeTypeConfiguration(Type type)
        {
            var typeConfiguration = this.GetTypeConfiguration(type);
            var baseTypeConfiguration = IsSubclassOfRawGeneric(typeof(ValueObject<>), type.BaseType) ? new ValueObjectConfiguration() : this.GetConfiguration(type.BaseType);

            var config = ValueObjectConfiguration.Merge(typeConfiguration, baseTypeConfiguration);
            config.RuntimeType = type;

            return config;
        }

        private ValueObjectConfiguration GetTypeConfiguration(Type type)
        {
            var bootstrapperConfiguration = this.bootstrapper.GetConfiguration(type);
            var typeAnalyzerConfiguration = this.typeAnalyzer.GetConfiguration(type);

            return ValueObjectConfiguration.Combine(bootstrapperConfiguration, typeAnalyzerConfiguration);
        }
    }
}
