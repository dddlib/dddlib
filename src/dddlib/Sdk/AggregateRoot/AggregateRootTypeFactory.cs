// <copyright file="AggregateRootTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using dddlib.Sdk;

    internal class AggregateRootTypeFactory : IAggregateRootTypeFactory
    {
        public AggregateRootType Create(AggregateRootConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            var eventDispatcher = new DefaultEventDispatcher(configuration.RuntimeType);

            return new AggregateRootType(configuration.RuntimeType, configuration.UninitializedFactory, eventDispatcher);
        }
    }
}
