// <copyright file="ConfigureEntity.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Configuration
{
    using System;

    internal class ConfigureEntity<T> : IConfigureEntity<T>
        where T : Entity
    {
        public ConfigureEntity(AssemblyConfiguration configuration)
        {
        }

        public IConfigureEntity<T> ToUseNaturalKey<TKey>(Func<T, TKey> naturalKeySelector)
        {
            return this;
        }

        public IConfigureEntity<T> ToUseNaturalKey<TKey>(Func<T, TKey> naturalKeySelector, System.Collections.Generic.IEqualityComparer<TKey> equalityComparer)
        {
            return this;
        }
    }
}
