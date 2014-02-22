// <copyright file="Runtime.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    /*  TODO (Cameron): 
        Consider not using an indexer. => maybe GetEntityInfo, GetAggregateInfo, GetValueObjectInfo to include type check?
        Wrap calls that may fail in a try...catch block.
        Consider folding into Application.
        Consider using Guard clause.  */

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

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

                var configurationProvider = this.configurationProviderFactory(type);
                var configuration = configurationProvider.GetConfiguration(type);

                this.typeDescriptors.Add(type, typeDescriptor = new TypeAnalyzer(configuration).GetDescriptor(type));

                return typeDescriptor;
            }
        }
    }
}
