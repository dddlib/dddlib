// <copyright file="NaturalKeySelector.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using dddlib.Runtime;

    internal class NaturalKeySelector : IEquatable<NaturalKeySelector>
    {
        public static readonly NaturalKeySelector Undefined = new NaturalKeySelector();

        private readonly string propertyName;
        private readonly Type runtimeType;
        private readonly Type returnType;
        private readonly Func<Entity, object> selector;

        public NaturalKeySelector(Type runtimeType, string propertyName)
        {
            Guard.Against.Null(() => runtimeType);
            Guard.Against.NullOrEmpty(() => propertyName);

            // TODO (Cameron): Consider whether this should work on non-entity types.
            if (!runtimeType.InheritsFrom(typeof(Entity)))
            {
                throw new RuntimeException(string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not an entity.", runtimeType));
            }

            var naturalKey = default(PropertyInfo);
            foreach (var subType in new[] { runtimeType }.Traverse(t => t.BaseType == typeof(Entity) || t.BaseType == typeof(Entity) ? null : new[] { t.BaseType }))
            {
                naturalKey = subType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                    .Where(member => member.Name == propertyName)
                    .SingleOrDefault();

                if (naturalKey != null)
                {
                    break;
                }
            }

            this.runtimeType = runtimeType;
            this.propertyName = propertyName;
            this.returnType = naturalKey.PropertyType;

            var parameter = Expression.Parameter(runtimeType, "entity");
            var property = Expression.Property(parameter, naturalKey);
            var funcType = typeof(Func<,>).MakeGenericType(runtimeType, naturalKey.PropertyType);
            var lambda = Expression.Lambda(funcType, property, parameter);
            var sourceParameter = Expression.Parameter(typeof(object), "source");
            var result = Expression.Lambda<Func<object, object>>(
                Expression.Invoke(
                    lambda,
                    Expression.Convert(sourceParameter, runtimeType)),
                sourceParameter);

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

        public Type ReturnType
        {
            get { return this.returnType; }
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
