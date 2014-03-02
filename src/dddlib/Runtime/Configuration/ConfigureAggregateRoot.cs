// <copyright file="ConfigureAggregateRoot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Configuration
{
    using System;

    internal class ConfigureAggregateRoot<T> : IConfigureAggregateRoot<T> 
        where T : AggregateRoot
    {
        private readonly RuntimeConfiguration configuration;

        public ConfigureAggregateRoot(RuntimeConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            this.configuration = configuration;
        }

        public IConfigureAggregateRoot<T> ToReconstituteUsing(Func<T> uninitializedFactory)
        {
            this.configuration.RegisterAggregateRootFactory(uninitializedFactory);
            return this;
        }

        public IConfigureAggregateRoot<T> ToUseNaturalKey(Func<T, object> naturalKeySelector)
        {
            this.configuration.RegisterNaturalKeySelector(naturalKeySelector);
            return this;
        }
    }
}
