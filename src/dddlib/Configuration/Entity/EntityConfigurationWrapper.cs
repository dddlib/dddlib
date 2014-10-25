// <copyright file="EntityConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using dddlib.Runtime;

    internal class EntityConfigurationWrapper<T> : IEntityConfigurationWrapper<T>
        where T : Entity
    {
        private readonly EntityConfiguration configuration;

        public EntityConfigurationWrapper(EntityConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            this.configuration = configuration;
        }

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

        public IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<TEvent, T> mapping)
        {
            Guard.Against.Null(() => mapping);

            // TODO (Cameron): Some expression based stuff here to negate the need to wrap.
            ////this.configuration.Mapper = type => mapping((T)type);
            return this;
        }
    }
}
