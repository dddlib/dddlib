// <copyright file="AggregateRootConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Model.BootstrapperService
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using dddlib.Configuration;
    using dddlib.Sdk.Configuration.Model;

    internal class AggregateRootConfigurationWrapper<T> : IAggregateRootConfigurationWrapper<T> 
        where T : AggregateRoot
    {
        private readonly AggregateRootType aggregateRootType;
        private readonly IEntityConfigurationWrapper<T> entityConfigurationWrapper;

        public AggregateRootConfigurationWrapper(AggregateRootType aggregateRootType, IEntityConfigurationWrapper<T> entityConfigurationWrapper)
        {
            Guard.Against.Null(() => aggregateRootType);
            Guard.Against.Null(() => entityConfigurationWrapper);

            this.aggregateRootType = aggregateRootType;
            this.entityConfigurationWrapper = entityConfigurationWrapper;
        }

        public IAggregateRootConfigurationWrapper<T> ToReconstituteUsing(Func<T> uninitializedFactory)
        {
            this.aggregateRootType.ConfigureUnititializedFactory(uninitializedFactory);

            return this;
        }

        public IAggregateRootConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
        {
            this.entityConfigurationWrapper.ToUseNaturalKey(naturalKeySelector);

            return this;
        }

        public IAggregateRootConfigurationWrapper<T> ToUseNaturalKey(
            Expression<Func<T, string>> naturalKeySelector, IEqualityComparer<string> equalityComparer)
        {
            this.entityConfigurationWrapper.ToUseNaturalKey(naturalKeySelector, equalityComparer);

            return this;
        }
    }
}
