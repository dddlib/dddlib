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
        public static readonly NaturalKeySelector Undefined = new NaturalKeySelector();

        private readonly string propertyName;
        private readonly Type runtimeType;
        private readonly Func<Entity, object> selector;

        public NaturalKeySelector(Type runtimeType, string propertyName)
        {
            Guard.Against.Null(() => runtimeType);
            Guard.Against.NullOrEmpty(() => propertyName);

            this.runtimeType = runtimeType;
            this.propertyName = propertyName;

            ////if (runtimeType == null || propertyName == null)
            ////{
            ////    this.runtimeType = typeof(object);
            ////    this.propertyName = string.Empty;
            ////    return;
            ////}

            var naturalKey = default(PropertyInfo);
            foreach (var subType in new[] { runtimeType }.Traverse(t => t.BaseType == typeof(Entity) ? null : new[] { t.BaseType }))
            {
                naturalKey = subType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                    .Where(member => member.Name == propertyName)
                    .SingleOrDefault();

                if (naturalKey != null)
                {
                    break;
                }
            }

            var parameter = Expression.Parameter(runtimeType, "entity");
            var property = Expression.Property(parameter, naturalKey);
            var funcType = typeof(Func<,>).MakeGenericType(runtimeType, naturalKey.PropertyType);
            var lambda = Expression.Lambda(funcType, property, parameter);

            ParameterExpression sourceParameter = Expression.Parameter(typeof(object), "source");
            var result = Expression.Lambda<Func<object, object>>(
                Expression.Invoke(
                    lambda,
                    Expression.Convert(sourceParameter, runtimeType)),
                sourceParameter);

            ////var function = Delegate.CreateDelegate(typeof(Func<object, object>), type, naturalKey);;
            this.selector = result.Compile() as Func<Entity, object>;
        }

        private NaturalKeySelector()
        {
            this.runtimeType = typeof(object);
            this.propertyName = string.Empty;
        }

        public Type RuntimeType
        {
            get { return this.runtimeType; }
        }

        public override int GetHashCode()
        {
            return unchecked(this.propertyName.GetHashCode() + this.runtimeType.GetHashCode());
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

            return this.propertyName == other.propertyName && this.runtimeType == other.runtimeType;
        }

        public object Invoke(Entity entity)
        {
            // TODO (Cameron): This is rubbish.
            return this.selector == null ? Guid.NewGuid() : this.selector(entity);
        }
    }
}
