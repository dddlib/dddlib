// <copyright file="NaturalKey.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Model
{
    using System;
    using System.Globalization;
    using System.Linq.Expressions;

    /// <summary>
    /// Represents a natural key.
    /// </summary>
    public class NaturalKey : ValueObject<NaturalKey>
    {
        private static readonly DefaultValueObjectEqualityComparer<NaturalKey> DefaultEqualityComparer = new DefaultValueObjectEqualityComparer<NaturalKey>();

        private readonly Func<Entity, object> getValue = entity => null;

        /// <summary>
        /// Initializes a new instance of the <see cref="NaturalKey"/> class.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyType">The property type.</param>
        /// <param name="typeAnalyzerService">The type analyzer service.</param>
        //// TODO (Cameron): Should there be another overload?
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

        /// <summary>
        /// Gets the runtime type of the entity which has this natural key.
        /// </summary>
        /// <value>The runtime type.</value>
        public Type RuntimeType { get; private set; }

        /// <summary>
        /// Gets the property name.
        /// </summary>
        /// <value>The property name.</value>
        public string PropertyName { get; private set; }

        /// <summary>
        /// Gets the property type.
        /// </summary>
        /// <value>The property type.</value>
        public Type PropertyType { get; private set; }

        /// <summary>
        /// Gets the value of the natural key from the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>The natural key value.</returns>
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
