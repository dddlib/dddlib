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
        private readonly AggregateRootConfigurationProvider configurationProvider;

        public AggregateRootTypeFactory(AggregateRootConfigurationProvider configurationProvider)
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
                configuration = this.configurationProvider.Get(currentType);
                mappings.Add(currentType, configuration.ApplyMethodName ?? "Apply");
            }

            // create type
            return new AggregateRootType
            {
                Factory = configuration.Factory,
                EventDispatcher = new DefaultEventDispatcher(type),
                Options = new AggregateRootType.RuntimeOptions
                {
                    DispatchEvents = true, // unless there's no handlers for this type at all
                    PersistEvents = configuration.Factory != null,
                }
            };
        }
    }
}
