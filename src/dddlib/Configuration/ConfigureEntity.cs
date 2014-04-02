// <copyright file="ConfigureEntity.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
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

        public IConfigureEntity<T> ToUseNaturalKey(Func<T, string> naturalKeySelector, System.Collections.Generic.IEqualityComparer<string> equalityComparer)
        {
            return this;
        }
    }
}
