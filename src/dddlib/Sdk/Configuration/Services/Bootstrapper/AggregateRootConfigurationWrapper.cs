// <copyright file="AggregateRootConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Services.Bootstrapper
{
    using System;
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
            this.aggregateRootType.ConfigureUninitializedFactory(uninitializedFactory);

            return this;
        }

        public IAggregateRootConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
        {
            this.entityConfigurationWrapper.ToUseNaturalKey(naturalKeySelector);

            return this;
        }
    }
}
