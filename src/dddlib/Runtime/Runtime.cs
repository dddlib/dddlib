// <copyright file="Runtime.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    /*  TODO (Cameron): 
        Consider folding into Application.  */

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    internal class Runtime
    {
        private readonly Dictionary<Type, TypeDescriptor> typeDescriptors = new Dictionary<Type, TypeDescriptor>();

        private readonly Func<Type, ITypeConfigurationProvider> configurationProviderFactory;

        public Runtime(Func<Type, ITypeConfigurationProvider> configurationProviderFactory)
        {
            Guard.Against.Null(() => configurationProviderFactory);

            this.configurationProviderFactory = configurationProviderFactory;
        }

        public TypeDescriptor GetAggregateRootDescriptor(Type type)
        {
            return this.GetDescriptor(type, typeof(AggregateRoot));
        }

        public TypeDescriptor GetEntityDescriptor(Type type)
        {
            return this.GetDescriptor(type, typeof(Entity));
        }

        public TypeDescriptor GetValueObjectDescriptor(Type type)
        {
            return this.GetDescriptor(type, typeof(ValueObject<>));
        }

        private TypeDescriptor GetDescriptor(Type type, Type descriptorType)
        {
            Guard.Against.Null(() => type);

            if (!descriptorType.IsAssignableFrom(type))
            {
                throw new ArgumentException("Invalid runtime type specified.", "type");
            }

            var typeDescriptor = default(TypeDescriptor);
            if (this.typeDescriptors.TryGetValue(type, out typeDescriptor))
            {
                return typeDescriptor;
            }

            lock (this.typeDescriptors)
            {
                if (this.typeDescriptors.TryGetValue(type, out typeDescriptor))
                {
                    return typeDescriptor;
                }

                var typeConfigurationProvider = default(ITypeConfigurationProvider);
                try
                {
                    typeConfigurationProvider = this.configurationProviderFactory(type);
                }
                catch (Exception ex)
                {
                    throw new RuntimeException(
                        "The type configuration provider factory threw an exception during invocation.\r\nSee inner exception for details.",
                        ex);
                }

                var typeConfiguration = default(TypeConfiguration);
                try
                {
                    typeConfiguration = typeConfigurationProvider.GetConfiguration(type);
                }
                catch (Exception ex)
                {
                    throw new RuntimeException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "The type configuration provider of type '{0}' threw an exception during invocation.\r\nSee inner exception for details.",
                            typeConfigurationProvider.GetType()),
                        ex);
                }

                this.typeDescriptors.Add(type, typeDescriptor = new TypeAnalyzer().GetDescriptor(type, typeConfiguration));

                return typeDescriptor;
            }
        }
    }
}
