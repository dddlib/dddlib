// <copyright file="AggregateRootTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class AggregateRootTypeFactory : ITypeFactory<AggregateRootType>
    {
        private readonly IAggregateRootConfigurationProvider configurationProvider;

        public AggregateRootTypeFactory(IAggregateRootConfigurationProvider configurationProvider)
        {
            Guard.Against.Null(() => configurationProvider);

            this.configurationProvider = configurationProvider;
        }

        public AggregateRootType Create(Type type)
        {
            var mappings = new Dictionary<Type, string>();

            // get configuration for all types in the hierarchy
            var configuration = default(AggregateRootConfiguration);

            foreach (var currentType in new[] { type }
                .Traverse(t => t.BaseType == typeof(AggregateRoot) ? null : new[] { t.BaseType })
                .Reverse())
            {
                configuration = this.configurationProvider.GetConfiguration(currentType);
                ////mappings.Add(currentType, configuration.ApplyMethodName ?? "Apply");
                mappings.Add(currentType, "Handle");
            }

            // TODO (Cameron): Consider moving some of this into the type itself.
            return new AggregateRootType(configuration.UninitializedFactory, new DefaultEventDispatcher(type));
        }
    }
}
