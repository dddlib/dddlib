// <copyright file="BootstrapperConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using dddlib.Configuration;
    using dddlib.Sdk.Configuration.Model;

    // TODO (Cameron): I've made a number of changes here that need to get addressed.
    // 1. The types that we create here will break due to inheritance issues.
    // 2. The return types can now be null.
    internal class BootstrapperConfiguration : IConfiguration
    {
        private readonly AggregateRootType aggregateRootType;
        private readonly EntityType entityType;
        private readonly ValueObjectType valueObjectType;
        private readonly ITypeAnalyzerService typeAnalyzerService;

        public BootstrapperConfiguration(AggregateRootType aggregateRootType, ITypeAnalyzerService typeAnalyzerService)
        {
            Guard.Against.Null(() => aggregateRootType);
            Guard.Against.Null(() => typeAnalyzerService);

            this.aggregateRootType = aggregateRootType;
            this.entityType = aggregateRootType;
            this.typeAnalyzerService = typeAnalyzerService;
        }

        public BootstrapperConfiguration(EntityType entityType, ITypeAnalyzerService typeAnalyzerService)
        {
            Guard.Against.Null(() => entityType);
            Guard.Against.Null(() => typeAnalyzerService);

            this.entityType = entityType;
            this.typeAnalyzerService = typeAnalyzerService;
        }

        public BootstrapperConfiguration(ValueObjectType valueObjectType, ITypeAnalyzerService typeAnalyzerService)
        {
            Guard.Against.Null(() => valueObjectType);
            Guard.Against.Null(() => typeAnalyzerService);

            this.valueObjectType = valueObjectType;
            this.typeAnalyzerService = typeAnalyzerService;
        }

        public IAggregateRootConfigurationWrapper<T> AggregateRoot<T>() where T : AggregateRoot
        {
            if (this.aggregateRootType != null && this.aggregateRootType.RuntimeType == typeof(T))
            {
                return new AggregateRootConfigurationWrapper<T>(this.aggregateRootType, this.Entity<T>());
            }

            return new EmptyAggregateRootConfigurationWrapper<T>(this.Entity<T>());
        }

        public IEntityConfigurationWrapper<T> Entity<T>() where T : Entity
        {
            if (this.entityType != null && this.entityType.RuntimeType == typeof(T))
            {
                return new EntityConfigurationWrapper<T>(this.entityType, this.typeAnalyzerService);
            }

            return new EmptyEntityConfigurationWrapper<T>();
        }

        public IValueObjectConfigurationWrapper<T> ValueObject<T>() where T : ValueObject<T>
        {
            if (this.valueObjectType != null && this.valueObjectType.RuntimeType == typeof(T))
            {
                return new ValueObjectConfigurationWrapper<T>(this.valueObjectType);
            }

            return new EmptyValueObjectConfigurationWrapper<T>();
        }

        private class EmptyAggregateRootConfigurationWrapper<T> : IAggregateRootConfigurationWrapper<T>
            where T : AggregateRoot
        {
            private readonly IEntityConfigurationWrapper<T> entityConfigurationWrapper;

            public EmptyAggregateRootConfigurationWrapper(IEntityConfigurationWrapper<T> entityConfigurationWrapper)
            {
                Guard.Against.Null(() => entityConfigurationWrapper);

                this.entityConfigurationWrapper = entityConfigurationWrapper;
            }

            public IAggregateRootConfigurationWrapper<T> ToReconstituteUsing(Func<T> uninitializedFactory)
            {
                return this;
            }

            public IAggregateRootConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
            {
                this.entityConfigurationWrapper.ToUseNaturalKey(naturalKeySelector);
                return this;
            }

            public IAggregateRootConfigurationWrapper<T> ToUseNaturalKey(Expression<Func<T, string>> naturalKeySelector, IEqualityComparer<string> equalityComparer)
            {
                this.entityConfigurationWrapper.ToUseNaturalKey(naturalKeySelector, equalityComparer);
                return this;
            }
        }

        private class EmptyEntityConfigurationWrapper<T> : IEntityConfigurationWrapper<T>
            where T : Entity
        {
            public IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping)
            {
                return this;
            }

            public IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping)
            {
                return this;
            }

            public IEntityConfigurationWrapper<T> ToUseNaturalKey(Expression<Func<T, string>> naturalKeySelector, IEqualityComparer<string> equalityComparer)
            {
                return this;
            }

            public IEntityConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
            {
                return this;
            }
        }

        private class EmptyValueObjectConfigurationWrapper<T> : IValueObjectConfigurationWrapper<T>
            where T : ValueObject<T>
        {
            public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping)
            {
                return this;
            }

            public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping)
            {
                return this;
            }

            public IValueObjectConfigurationWrapper<T> ToUseEqualityComparer(IEqualityComparer<T> equalityComparer)
            {
                return this;
            }
        }
    }
}
