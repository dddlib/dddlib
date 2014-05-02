// <copyright file="ConfigureEntity.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using dddlib.Runtime;

    internal class ConfigureEntity<T> : IConfigureEntity<T>
        where T : Entity
    {
        private readonly EntityConfiguration configuration;

        public ConfigureEntity(EntityConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            this.configuration = configuration;
        }

        public IConfigureEntity<T> ToUseNaturalKey<TKey>(Func<T, TKey> naturalKeySelector)
        {
            // TODO (Cameron): Some expression based stuff here to negate the need to wrap.
            this.configuration.NaturalKeySelector = type => naturalKeySelector((T)type);
            return this;
        }

        public IConfigureEntity<T> ToUseNaturalKey(Func<T, string> naturalKeySelector, IEqualityComparer<string> equalityComparer)
        {
            // TODO (Cameron): Some expression based stuff here to negate the need to wrap.
            this.configuration.NaturalKeySelector = type => naturalKeySelector((T)type);
            this.configuration.NaturalKeyStringEqualityComparer = equalityComparer;
            return this;
        }
    }
}
