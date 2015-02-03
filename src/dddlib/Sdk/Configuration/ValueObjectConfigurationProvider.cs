// <copyright file="ValueObjectConfigurationProvider.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Collections.Generic;

    internal class ValueObjectConfigurationProvider
    {
        private readonly Dictionary<Type, ValueObjectConfiguration> config = new Dictionary<Type, ValueObjectConfiguration>();

        private readonly IBootstrapperProvider bootstrapperProvider;

        public ValueObjectConfigurationProvider(IBootstrapperProvider bootstrapperProvider)
        {
            Guard.Against.Null(() => bootstrapperProvider);

            this.bootstrapperProvider = bootstrapperProvider;
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
            var bootstrapper = this.bootstrapperProvider.GetBootstrapper(type);

            // TODO (Cameron): This should be an injected configuration collection.
            var configuration = new BootstrapperConfiguration();

            bootstrapper.Invoke(configuration);

            var typeConfiguration = configuration.GetValueObjectConfiguration(type);
            var baseTypeConfiguration = IsSubclassOfRawGeneric(typeof(ValueObject<>), type.BaseType) ? new ValueObjectConfiguration() : this.GetConfiguration(type.BaseType);

            return ValueObjectConfiguration.Merge(typeConfiguration, baseTypeConfiguration);
        }
    }
}
