// <copyright file="EntityConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using dddlib.Configuration;
    using dddlib.Sdk;

    internal class EntityConfigurationWrapper<T> : IEntityConfigurationWrapper<T>
        where T : Entity
    {
        private readonly EntityConfiguration configuration;

        public EntityConfigurationWrapper(EntityConfiguration configuration)
        {
            Guard.Against.Null(() => configuration);

            this.configuration = configuration;

            // TODO (Cameron): Not sure this belongs here...
            if (this.configuration.Mappings == null)
            {
                this.configuration.Mappings = new MapperCollection();
            }
        }

        public IEntityConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
        {
            Guard.Against.InvalidMemberExpression(() => naturalKeySelector, out var memberExpression);

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

        public IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping)
        {
            Guard.Against.Null(() => mapping);

            this.configuration.Mappings.AddOrUpdate(mapping);

            return this;
        }

        public IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping)
        {
            Guard.Against.Null(() => mapping);
            Guard.Against.Null(() => reverseMapping);

            this.configuration.Mappings.AddOrUpdate(mapping);
            this.configuration.Mappings.AddOrUpdate(reverseMapping);

            return this;
        }
    }
}
