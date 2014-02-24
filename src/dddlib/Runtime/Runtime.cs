// <copyright file="Runtime.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    /*  TODO (Cameron): 
        Consider folding into Application.
        Consider renaming and moving the SafeInvoke wrapper for the try...catch blocks into a separate class for reuse elsewhere.  */

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

        private static T SafeInvoke<T>(Func<T> thing, string errorMessgae, params object[] errorMessgaeArguments)
        {
            try
            {
                return thing();
            }
            catch (Exception ex)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        string.Concat(errorMessgae, "\r\nSee inner exception for details."),
                        errorMessgaeArguments),
                    ex);
            }
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

                var typeConfigurationProvider = SafeInvoke(
                    () => this.configurationProviderFactory(type), 
                    "The type configuration provider factory threw an exception during invocation.");

                var typeConfiguration = SafeInvoke(
                    () => typeConfigurationProvider.GetConfiguration(type),
                    "The type configuration provider of type '{0}' threw an exception during invocation.", 
                    typeConfigurationProvider.GetType());

                this.typeDescriptors.Add(type, typeDescriptor = new TypeAnalyzer().GetDescriptor(type, typeConfiguration));

                return typeDescriptor;
            }
        }
    }
}
