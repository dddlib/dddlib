// <copyright file="AggregateRootConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using dddlib.Configuration;

    internal class AggregateRootConfigurationWrapper<T> : IAggregateRootConfigurationWrapper<T> 
        where T : AggregateRoot
    {
        private readonly AggregateRootConfiguration configuration;
        private readonly IEntityConfigurationWrapper<T> entityConfig;

        public AggregateRootConfigurationWrapper(AggregateRootConfiguration configuration, IEntityConfigurationWrapper<T> entityConfig)
        {
            Guard.Against.Null(() => configuration);
            Guard.Against.Null(() => entityConfig);

            this.configuration = configuration;
            this.entityConfig = entityConfig;
        }

        public IAggregateRootConfigurationWrapper<T> ToReconstituteUsing(Func<T> uninitializedFactory)
        {
            this.configuration.UninitializedFactory = uninitializedFactory;

            return this;
        }

        public IAggregateRootConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
        {
            this.entityConfig.ToUseNaturalKey(naturalKeySelector);

            return this;
        }

        public IAggregateRootConfigurationWrapper<T> ToUseNaturalKey(
            Expression<Func<T, string>> naturalKeySelector, IEqualityComparer<string> equalityComparer)
        {
            this.entityConfig.ToUseNaturalKey(naturalKeySelector, equalityComparer);

            return this;
        }
    }
}
