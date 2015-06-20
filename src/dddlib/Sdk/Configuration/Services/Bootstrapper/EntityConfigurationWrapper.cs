// <copyright file="EntityConfigurationWrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Services.Bootstrapper
{
    using System;
    using System.Linq.Expressions;
    using dddlib.Configuration;
    using dddlib.Sdk.Configuration.Model;

    internal class EntityConfigurationWrapper<T> : IEntityConfigurationWrapper<T>
        where T : Entity
    {
        private readonly EntityType entityType;
        private readonly ITypeAnalyzerService typeAnalyzerService;

        public EntityConfigurationWrapper(EntityType entityType, ITypeAnalyzerService typeAnalyzerService)
        {
            Guard.Against.Null(() => entityType);
            Guard.Against.Null(() => typeAnalyzerService);

            this.entityType = entityType;
            this.typeAnalyzerService = typeAnalyzerService;
        }

        public IEntityConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
        {
            var memberExpression = default(MemberExpression);
            Guard.Against.InvalidMemberExpression(() => naturalKeySelector, out memberExpression);

            var naturalKey = new NaturalKey(typeof(T), memberExpression.Member.Name, typeof(TKey), this.typeAnalyzerService);
            this.entityType.ConfigureNaturalKey(naturalKey);

            return this;
        }

        public IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping)
        {
            Guard.Against.Null(() => mapping);

            this.entityType.Mappings.AddOrUpdate(mapping);

            return this;
        }

        public IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping)
        {
            Guard.Against.Null(() => mapping);
            Guard.Against.Null(() => reverseMapping);

            this.entityType.Mappings.AddOrUpdate(mapping);
            this.entityType.Mappings.AddOrUpdate(reverseMapping);

            return this;
        }
    }
}
