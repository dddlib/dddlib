// <copyright file="ValueObjectConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class ValueObjectConfigurationProvider
    {
        private readonly Dictionary<Type, ValueObjectConfiguration> config = new Dictionary<Type, ValueObjectConfiguration>();

        private readonly Bootstrapper bootstrapper;
        private readonly ValueObjectAnalyzer typeAnalyzer;
        private readonly ValueObjectConfigurationManager manager;

        public ValueObjectConfigurationProvider(Bootstrapper bootstrapper, ValueObjectAnalyzer typeAnalyzer, ValueObjectConfigurationManager manager)
        {
            this.bootstrapper = bootstrapper;
            this.typeAnalyzer = typeAnalyzer;
            this.manager = manager;
        }

        public ValueObjectConfiguration Get(Type type)
        {
            if (!typeof(ValueObject<>).IsAssignableFrom(type))
            {
                throw new Exception("not an aggregate!");
            }

            var runtimeTypeConfiguration = default(ValueObjectConfiguration);
            if (!this.config.TryGetValue(type, out runtimeTypeConfiguration))
            {
                this.config.Add(type, runtimeTypeConfiguration = this.GetRuntimeTypeConfiguration(type));
            }

            return runtimeTypeConfiguration;
        }

        private ValueObjectConfiguration GetRuntimeTypeConfiguration(Type type)
        {
            var typeConfiguration = this.GetTypeConfiguration(type);
            var baseTypeConfiguration = type.BaseType == typeof(ValueObject<>) ? new ValueObjectConfiguration() : this.Get(type.BaseType);

            return this.manager.Merge(typeConfiguration, baseTypeConfiguration);
        }

        private ValueObjectConfiguration GetTypeConfiguration(Type type)
        {
            var bootstrapperConfiguration = this.bootstrapper.GetValueObjectConfiguration(type);
            var typeAnalyzerConfiguration = this.typeAnalyzer.GetConfiguration(type);

            return new[] { bootstrapperConfiguration, typeAnalyzerConfiguration }.Combine();
        }
    }
}
