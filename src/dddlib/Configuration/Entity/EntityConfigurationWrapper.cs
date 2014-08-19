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
            Guard.Against.InvalidExpression(() => naturalKeySelector, out var expression);

            this.configuration.EntityType = typeof(T);
            this.configuration.NaturalKeyPropertyName = expression.Member.Name;

            return this;
        }

        public IEntityConfigurationWrapper<T> ToUseNaturalKey(
            Expression<Func<T, string>> naturalKeySelector, 
            IEqualityComparer<string> equalityComparer)
        {
            Guard.Against.InvalidExpression(() => naturalKeySelector, out var expression);

            this.configuration.EntityType = typeof(T);
            this.configuration.NaturalKeyPropertyName = expression.Member.Name;
            this.configuration.NaturalKeyStringEqualityComparer = equalityComparer;

            return this;
        }
    }
}
