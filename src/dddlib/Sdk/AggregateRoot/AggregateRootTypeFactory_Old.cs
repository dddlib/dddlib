// <copyright file="AggregateRootTypeFactory_Old.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

    internal class AggregateRootTypeFactory_Old : ITypeFactory<AggregateRootType>
    {
        private readonly AggregateRootTypeFactory factory = new AggregateRootTypeFactory();
        private readonly IAggregateRootConfigurationProvider configurationProvider;

        public AggregateRootTypeFactory_Old(IAggregateRootConfigurationProvider configurationProvider)
        {
            Guard.Against.Null(() => configurationProvider);

            this.configurationProvider = configurationProvider;
        }

        public AggregateRootType Create(Type type)
        {
            ////var mappings = new Dictionary<Type, string>();

            ////// get configuration for all types in the hierarchy
            ////var configuration = default(AggregateRootConfiguration);

            ////foreach (var currentType in new[] { type }
            ////    .Traverse(t => t.BaseType == typeof(AggregateRoot) ? null : new[] { t.BaseType })
            ////    .Reverse())
            ////{
            ////    configuration = this.configurationProvider.GetConfiguration(currentType);
            ////    ////mappings.Add(currentType, configuration.ApplyMethodName ?? "Apply");
            ////    mappings.Add(currentType, "Handle");
            ////}

            var configuration = this.configurationProvider.GetConfiguration(type);
            configuration.RuntimeType = type;

            // TODO (Cameron): Consider moving some of this into the type itself.
            return this.factory.Create(configuration);
        }
    }
}
