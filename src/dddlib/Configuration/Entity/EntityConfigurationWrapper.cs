// <copyright file="EntityConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using dddlib.Runtime;

    internal class EntityConfigurationWrapper<T>(private EntityConfiguration configuration) : IEntityConfigurationWrapper<T>
        where T : Entity
    {
        public IEntityConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
        {
            Guard.Against.InvalidMemberExpression(() => naturalKeySelector, out var memberExpression);

            this.configuration.EntityType = typeof(T);
            this.configuration.NaturalKeyPropertyName = memberExpression.Member.Name;

            return this;
        }

        public IEntityConfigurationWrapper<T> ToUseNaturalKey(
            Expression<Func<T, string>> naturalKeySelector, IEqualityComparer<string> equalityComparer)
        {
            Guard.Against.Null(() => equalityComparer);

            this.ToUseNaturalKey(naturalKeySelector);
            this.configuration.NaturalKeyStringEqualityComparer = equalityComparer;

            return this;
        }
    }
}
