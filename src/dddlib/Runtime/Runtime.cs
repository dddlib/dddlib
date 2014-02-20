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

        private readonly Dictionary<Assembly, Configuration> assemblyConfigurations = new Dictionary<Assembly, Configuration>();
        private readonly Dictionary<Type, TypeDescriptor> typeDescriptors = new Dictionary<Type, TypeDescriptor>();

        public Configuration this[Assembly assembly]
        {
            get
            {
                var assemblyConfiguration = default(Configuration);
                if (this.assemblyConfigurations.TryGetValue(assembly, out assemblyConfiguration))
                {
                    return assemblyConfiguration;
                }

                lock (this.assemblyConfigurations)
                {
                    if (this.assemblyConfigurations.TryGetValue(assembly, out assemblyConfiguration))
                    {
                        return assemblyConfiguration;
                    }

                    this.assemblyConfigurations.Add(assembly, assemblyConfiguration = new ConfigurationProvider().GetConfiguration(assembly));

                    return assemblyConfiguration;
                }
            }
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

                    this.typeDescriptors.Add(type, typeDescriptor = new TypeAnalyzer(this[type.Assembly]).GetDescriptor(type));

                    return typeDescriptor;
                }
            }
        }
    }
}
