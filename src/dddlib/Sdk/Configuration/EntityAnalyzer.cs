// <copyright file="EntityAnalyzer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using dddlib.Sdk;

    internal class EntityAnalyzer : IEntityConfigurationProvider
    {
        public EntityConfiguration GetConfiguration(Type type)
        {
            var naturalKey = default(PropertyInfo);
            foreach (var subType in new[] { type }.Traverse(t => t.BaseType == typeof(Entity) ? null : new[] { t.BaseType }))
            {
                naturalKey = subType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                    .Where(member => member.GetCustomAttributes(typeof(NaturalKey), true).SingleOrDefault() != null)
                    .SingleOrDefault();

                if (naturalKey != null)
                {
                    break;
                }
            }

            if (naturalKey == null) 
            {
                // TODO (Cameron): Move into AggregateRootTypeAnalyzer.
                ////if (configuration.AggregateRootFactory != null)
                ////{
                ////    throw new RuntimeException(
                ////        string.Format(CultureInfo.InvariantCulture, "The entity of type '{0}' does not have a natural key defined.", type.Name));
                ////}

                return new EntityConfiguration();
            }

            var parameter = Expression.Parameter(type, "entity");
            var property = Expression.Property(parameter, naturalKey);
            var funcType = typeof(Func<,>).MakeGenericType(type, naturalKey.PropertyType);
            var lambda = Expression.Lambda(funcType, property, parameter);

            ParameterExpression sourceParameter = Expression.Parameter(typeof(object), "source");
            var result = Expression.Lambda<Func<object, object>>(
                Expression.Invoke(
                    lambda,
                    Expression.Convert(sourceParameter, type)),
                sourceParameter);

            ////var function = Delegate.CreateDelegate(typeof(Func<object, object>), type, naturalKey);;
            var function = result.Compile() as Func<object, object>;

            // TODO (Cameron): Get equality comparer from config.
            return new EntityConfiguration
            {
                NaturalKeyPropertyName = naturalKey.Name,
                RuntimeType = type,
            };
        }
    }
}
