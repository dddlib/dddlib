// <copyright file="NaturalKey.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Model
{
    using System;
    using System.Globalization;
    using System.Linq.Expressions;

    internal class NaturalKey : ValueObject<NaturalKey>
    {
        private static readonly DefaultValueObjectEqualityComparer<NaturalKey> DefaultEqualityComparer = new DefaultValueObjectEqualityComparer<NaturalKey>();

        private readonly Func<Entity, object> getValue = entity => null;

        // TODO (Cameron): Should there be another overload?
        public NaturalKey(Type runtimeType, string propertyName, Type propertyType, ITypeAnalyzerService typeAnalyzerService)
            : base(DefaultEqualityComparer)
        {
            Guard.Against.Null(() => runtimeType);
            Guard.Against.Null(() => propertyName); // TODO (Cameron): Or empty!
            Guard.Against.Null(() => propertyType);
            Guard.Against.Null(() => typeAnalyzerService);

            if (!typeAnalyzerService.IsValidEntity(runtimeType))
            {
                throw new BusinessException(
                    string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not an entity.", runtimeType));
            }

            if (!typeAnalyzerService.IsValidProperty(runtimeType, propertyName, propertyType))
            {
                throw new BusinessException(
                    string.Format(
                        CultureInfo.InvariantCulture, 
                        "Invalid natural key specification. The entity '{0}' does not contain a property named '{1}' of type '{2}'.", 
                        runtimeType,
                        propertyName,
                        propertyType));
            }

            this.PropertyName = propertyName;
            this.PropertyType = propertyType;
            this.RuntimeType = runtimeType;
            this.getValue = AssignGetValue(runtimeType, propertyName);
        }

        private NaturalKey()
            : base(new DefaultValueObjectEqualityComparer<NaturalKey>())
        {
            this.getValue = entity => null;
        }

        public Type RuntimeType { get; private set; }

        public string PropertyName { get; private set; }

        public Type PropertyType { get; private set; }

        public object GetValue(Entity entity)
        {
            Guard.Against.Null(() => entity);

            return this.getValue(entity);
        }

        private static Func<Entity, object> AssignGetValue(Type runtimeType, string propertyName)
        {
            var sourceParameter = Expression.Parameter(typeof(object), "source");
            var parameter = Expression.Parameter(runtimeType, "entity");
            var property = Expression.Property(parameter, propertyName);
            var functionType = typeof(Func<,>).MakeGenericType(runtimeType, typeof(object));

            var lambda = property.Type.IsClass
                ? Expression.Lambda(functionType, property, parameter)
                : Expression.Lambda(functionType, Expression.Convert(property, typeof(object)), parameter);

            var result = Expression.Lambda<Func<object, object>>(
                Expression.Invoke(
                    lambda,
                    Expression.Convert(sourceParameter, runtimeType)),
                sourceParameter);

            return result.Compile() as Func<Entity, object>;
        }
    }
}
