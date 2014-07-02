// <copyright file="ConfigureAggregateRoot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using dddlib.Runtime;

    internal class ConfigureAggregateRoot<T> : IConfigureAggregateRoot<T> 
        where T : AggregateRoot
    {
        private readonly AggregateRootConfiguration configuration;
        private readonly IConfigureEntity<T> entityConfig;

        public ConfigureAggregateRoot(AggregateRootConfiguration configuration, IConfigureEntity<T> entityConfig)
        {
            Guard.Against.Null(() => configuration);
            Guard.Against.Null(() => entityConfig);

            this.configuration = configuration;
            this.entityConfig = entityConfig;
        }

        public IConfigureAggregateRoot<T> ToReconstituteUsing(Func<T> uninitializedFactory)
        {
            this.configuration.Factory = uninitializedFactory;
            return this;
        }

        public IConfigureAggregateRoot<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
        {
            this.entityConfig.ToUseNaturalKey(naturalKeySelector);
            return this;
        }

        public IConfigureAggregateRoot<T> ToUseNaturalKey(Expression<Func<T, string>> naturalKeySelector, IEqualityComparer<string> equalityComparer)
        {
            this.entityConfig.ToUseNaturalKey(naturalKeySelector, equalityComparer);
            return this;
        }
    }
}
