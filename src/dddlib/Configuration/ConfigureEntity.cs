// <copyright file="ConfigureEntity.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
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

        public IConfigureEntity<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
        {
            var expr = naturalKeySelector.Body as System.Linq.Expressions.MemberExpression;
            if (expr == null)
            {
                throw new Exception("not a memberexpression");
            }

            // TODO (Cameron): Some expression based stuff here to negate the need to wrap.
            this.configuration.EntityType = typeof(T);
            this.configuration.NaturalKeyPropertyName = expr.Member.Name;
            return this;
        }

        public IConfigureEntity<T> ToUseNaturalKey(Expression<Func<T, string>> naturalKeySelector, IEqualityComparer<string> equalityComparer)
        {
            var expr = naturalKeySelector.Body as System.Linq.Expressions.MemberExpression;
            if (expr == null)
            {
                throw new Exception("not a memberexpression");
            }

            // TODO (Cameron): Some expression based stuff here to negate the need to wrap.
            this.configuration.EntityType = typeof(T);
            this.configuration.NaturalKeyPropertyName = expr.Member.Name;
            this.configuration.NaturalKeyStringEqualityComparer = equalityComparer;
            return this;
        }
    }
}
