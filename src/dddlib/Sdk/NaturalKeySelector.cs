// <copyright file="NaturalKeySelector.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    internal class NaturalKeySelector : IEquatable<NaturalKeySelector>
    {
        private readonly string propertyName;
        private readonly Type type;
        private readonly Func<Entity, object> selector;

        public NaturalKeySelector(string propertyName, Type type)
        {
            this.propertyName = propertyName;
            this.type = type;

            if (type == null || propertyName == null)
            {
                this.propertyName = string.Empty;
                this.type = typeof(object);
                return;
            }

            var naturalKey = default(PropertyInfo);
            foreach (var subType in new[] { type }.Traverse(t => t.BaseType == typeof(Entity) ? null : new[] { t.BaseType }))
            {
                naturalKey = subType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                    .Where(member => member.Name == propertyName)
                    .SingleOrDefault();

                if (naturalKey != null)
                {
                    break;
                }
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
            this.selector = result.Compile() as Func<Entity, object>;
        }

        public override int GetHashCode()
        {
            return unchecked(this.propertyName.GetHashCode() + this.type.GetHashCode());
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as NaturalKeySelector);
        }

        public bool Equals(NaturalKeySelector other)
        {
            if (other == null)
            {
                return false;
            }

            return this.propertyName == other.propertyName && this.type == other.type;
        }

        public object Invoke(Entity entity)
        {
            // TODO (Cameron): This is rubbish.
            return this.selector == null ? Guid.NewGuid() : this.selector(entity);
        }
    }
}
