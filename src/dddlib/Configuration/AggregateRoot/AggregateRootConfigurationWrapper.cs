// <copyright file="AggregateRootConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using dddlib.Runtime;

    internal class AggregateRootConfigurationWrapper<T>(
        private AggregateRootConfiguration configuration, 
        private IEntityConfigurationWrapper<T> entityConfig) : IAggregateRootConfigurationWrapper<T> 
        where T : AggregateRoot
    {
        public IAggregateRootConfigurationWrapper<T> ToReconstituteUsing(Func<T> uninitializedFactory)
        {
            Guard.Against.Null(() => uninitializedFactory);

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
