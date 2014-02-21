// <copyright file="Runtime.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    internal class Runtime
    {
        private static readonly Type[] ValidTypes = new[] { typeof(AggregateRoot), typeof(Entity), typeof(ValueObject<>) };

        private readonly Dictionary<Type, TypeDescriptor> typeDescriptors = new Dictionary<Type, TypeDescriptor>();

        private readonly Func<Type, DefaultConfigurationProvider> configurationProviderFactory;

        public Runtime(Func<Type, DefaultConfigurationProvider> configurationProviderFactory)
        {
            Guard.Against.Null(() => configurationProviderFactory);

            this.configurationProviderFactory = configurationProviderFactory;
        }

        public TypeDescriptor this[Type type]
        {
            get
            {
                var typeDescriptor = default(TypeDescriptor);
                if (this.typeDescriptors.TryGetValue(type, out typeDescriptor))
                {
                    return typeDescriptor;
                }

                // NOTE (Cameron): This early check stops an assembly configuration being provided for an assembly with an invalid type.
                if (ValidTypes.Any(validType => type.IsAssignableFrom(validType)))
                {
                    throw new ArgumentException("Invalid runtime type specified.", "type");
                }

                lock (this.typeDescriptors)
                {
                    if (this.typeDescriptors.TryGetValue(type, out typeDescriptor))
                    {
                        return typeDescriptor;
                    }

                    var configurationProvider = this.configurationProviderFactory(type);
                    var configuration = configurationProvider.GetConfiguration(type);

                    this.typeDescriptors.Add(type, typeDescriptor = new TypeAnalyzer(configuration).GetDescriptor(type));

                    return typeDescriptor;
                }
            }
        }
    }
}
