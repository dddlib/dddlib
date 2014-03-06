// <copyright file="ConfigureAggregateRoot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;

    internal class ConfigureAggregateRoot<T> : IConfigureAggregateRoot<T> 
        where T : AggregateRoot
    {
        private readonly AssemblyConfiguration configuration;

        public ConfigureAggregateRoot(AssemblyConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            this.configuration = configuration;
        }

        public IConfigureAggregateRoot<T> ToReconstituteUsing(Func<T> uninitializedFactory)
        {
            this.configuration.RegisterAggregateRootFactory(uninitializedFactory);
            return this;
        }

        public IConfigureAggregateRoot<T> ToUseNaturalKey<TKey>(Func<T, TKey> naturalKeySelector)
        {
            this.configuration.RegisterNaturalKeySelector(naturalKeySelector);
            return this;
        }

        public IConfigureAggregateRoot<T> ToUseNaturalKey<TKey>(Func<T, TKey> naturalKeySelector, System.Collections.Generic.IEqualityComparer<TKey> equalityComparer)
        {
            this.configuration.RegisterNaturalKeySelector(naturalKeySelector);
            return this;
        }
    }
}
