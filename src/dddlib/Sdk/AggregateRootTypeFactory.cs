// <copyright file="AggregateRootTypeFactory.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using dddlib.Sdk.Configuration;

    internal class AggregateRootTypeFactory
    {
        public AggregateRootType Create(Type type, AggregateRootConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            var eventDispatcher = new DefaultEventDispatcher(type);

            return new AggregateRootType(type, configuration.UninitializedFactory, eventDispatcher);
        }
    }
}
