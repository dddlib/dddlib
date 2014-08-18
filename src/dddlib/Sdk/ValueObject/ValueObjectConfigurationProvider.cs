// <copyright file="ValueObjectConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class ValueObjectConfigurationProvider : IConfigurationProvider<ValueObjectConfiguration>
    {
        private readonly Dictionary<Type, ValueObjectConfiguration> config = new Dictionary<Type, ValueObjectConfiguration>();

        private readonly IConfigurationProvider<ValueObjectConfiguration> bootstrapper;
        private readonly IConfigurationProvider<ValueObjectConfiguration> typeAnalyzer;
        private readonly ValueObjectConfigurationManager manager;

        public ValueObjectConfigurationProvider(IConfigurationProvider<ValueObjectConfiguration> bootstrapper, IConfigurationProvider<ValueObjectConfiguration> typeAnalyzer, ValueObjectConfigurationManager manager)
        {
            this.bootstrapper = bootstrapper;
            this.typeAnalyzer = typeAnalyzer;
            this.manager = manager;
        }

        public ValueObjectConfiguration GetConfiguration(Type type)
        {
            ////if (!typeof(ValueObject<>).IsAssignableFrom(type))
            ////{
            ////    throw new Exception("not an aggregate!");
            ////}

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

            return this.manager.Merge(typeConfiguration, baseTypeConfiguration);
        }

        private ValueObjectConfiguration GetTypeConfiguration(Type type)
        {
            var bootstrapperConfiguration = this.bootstrapper.GetConfiguration(type);
            var typeAnalyzerConfiguration = this.typeAnalyzer.GetConfiguration(type);

            return new[] { bootstrapperConfiguration, typeAnalyzerConfiguration }.Combine();
        }
    }
}
