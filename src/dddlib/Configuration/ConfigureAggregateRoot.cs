// <copyright file="ConfigureAggregateRoot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using dddlib.Runtime;

    internal class ConfigureAggregateRoot<T> : IConfigureAggregateRoot<T> 
        where T : AggregateRoot
    {
        private readonly AggregateRootConfiguration configuration;

        public ConfigureAggregateRoot(AggregateRootConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            this.configuration = configuration;
        }

        public IConfigureAggregateRoot<T> ToReconstituteUsing(Func<T> uninitializedFactory)
        {
            this.configuration.Factory = uninitializedFactory;
            return this;
        }
    }
}
