// <copyright file="EntityType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Model
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    internal class EntityType : Entity
    {
        private static readonly ITypeAnalyzerService DefaultTypeAnalyzerService = new DefaultTypeAnalyzerService();

        public EntityType(Type runtimeType, ITypeAnalyzerService typeAnalyzerService)
             : base(new NaturalKey(typeof(EntityType), "RuntimeType", typeof(Type), DefaultTypeAnalyzerService), EqualityComparer<object>.Default)
        {
            Guard.Against.Null(() => runtimeType);
            Guard.Against.Null(() => typeAnalyzerService);

            if (!typeAnalyzerService.IsValidEntity(runtimeType))
            {
                throw new BusinessException(
                    string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not an entity.", runtimeType));
            }

            this.RuntimeType = runtimeType;
            this.NaturalKey = typeAnalyzerService.GetNaturalKey(runtimeType) ?? NaturalKey.Undefined;
            this.NaturalKeyEqualityComparer = EqualityComparer<object>.Default;
            this.Mappings = new MapperCollection();
        }

        public EntityType(Type runtimeType, ITypeAnalyzerService entityAnalyzerService, EntityType baseEntity)
            : this(runtimeType, entityAnalyzerService)
        {
            Guard.Against.Null(() => runtimeType);
            Guard.Against.Null(() => baseEntity);

            if (baseEntity.RuntimeType != runtimeType.BaseType)
            {
                throw new BusinessException(
                    string.Format(
                        CultureInfo.InvariantCulture, 
                        "The specified base entity runtime type '{0}' does not match the runtime type base type '{1}'.", 
                        baseEntity.RuntimeType,
                        runtimeType.BaseType));
            }

            if (this.NaturalKey == NaturalKey.Undefined)
            {
                this.NaturalKey = baseEntity.NaturalKey;
            }
        }

        [dddlib.NaturalKey]
        public Type RuntimeType { get; private set; }

        public NaturalKey NaturalKey { get; private set; }

        public IEqualityComparer<object> NaturalKeyEqualityComparer { get; private set; }

        // TODO (Cameron): This is not right. Law of Dementer and all that.
        public MapperCollection Mappings { get; private set; }

        public void ConfigureNaturalKey(NaturalKey naturalKey)
        {
            Guard.Against.Null(() => naturalKey);

            if (naturalKey == NaturalKey.Undefined)
            {
                throw new BusinessException("Cannot configure the natural key to be undefined.");
            }

            if (this.NaturalKey == naturalKey)
            {
                return;
            }

            if (naturalKey.RuntimeType != this.RuntimeType)
            {
                throw new BusinessException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid natural key specified. The natural key must have a declaring type of '{0}'.",
                        this.RuntimeType));
            }

            if (this.NaturalKey != NaturalKey.Undefined)
            {
                throw new BusinessException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cannot configure the natural key for the entity '{0}' to be the property '{1}' as the natural key is already configured to be the property '{2}'.",
                        this.RuntimeType,
                        naturalKey.PropertyName,
                        this.NaturalKey.PropertyName));
            }

            this.NaturalKey = naturalKey;
        }

        public void ConfigureNaturalKey(NaturalKey naturalKey, IEqualityComparer<string> naturalKeyEqualityComparer)
        {
            Guard.Against.Null(() => naturalKey);
            Guard.Against.Null(() => naturalKeyEqualityComparer);

            if (naturalKey.PropertyType != typeof(string))
            {
                throw new BusinessException("Cannot configure the equality comparer for a natural key with a return type other than 'System.String'.");
            }

            this.ConfigureNaturalKey(naturalKey);

            this.NaturalKeyEqualityComparer = new StringEqualityComparer(naturalKeyEqualityComparer);
        }
    }
}
