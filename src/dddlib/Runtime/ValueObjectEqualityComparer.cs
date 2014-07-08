// <copyright file="ValueObjectEqualityComparer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    internal class ValueObjectEqualityComparer<T> : IEqualityComparer<ValueObject<T>>
        where T : ValueObject<T>
    {
        private static readonly MethodInfo CastToObjects = typeof(object).MakeEnumerableCastMethod();
        private static readonly MethodInfo ObjectsEqual = typeof(object).MakeEnumerableSequenceEqualMethod();

        private readonly Func<ValueObject<T>, ValueObject<T>, bool> equalsMethod;
        private readonly Func<ValueObject<T>, int> hashCodeMethod;

        public ValueObjectEqualityComparer()
        {
            var type = typeof(T);

            var left = Expression.Parameter(typeof(ValueObject<T>), "left");
            var right = Expression.Parameter(typeof(ValueObject<T>), "right");

            var body = type.GetProperties()
                .Select(property => Generate(
                    property.PropertyType,
                    Expression.Property(Expression.Convert(left, type), property),
                    Expression.Property(Expression.Convert(right, type), property)))
                .Aggregate((Expression)Expression.Constant(true), (current, expression) => Expression.AndAlso(current, expression));

            this.equalsMethod = Expression.Lambda<Func<ValueObject<T>, ValueObject<T>, bool>>(body, left, right).Compile();

            var obj = Expression.Parameter(typeof(object), "obj");
            var body2 = type.GetProperties()
                .Select(property => Expression.Call(
                    this.GetType().GetMethod("GetHashCodeOrDefault", BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(property.PropertyType),
                    Expression.Property(Expression.TypeAs(obj, type), property)))
                .Aggregate(
                    (Expression)Expression.Constant(17),
                    (expression, propertyExpression) => Expression.Add(Expression.Multiply(expression, Expression.Constant(23)), propertyExpression));
             
            this.hashCodeMethod = Expression.Lambda<Func<ValueObject<T>, int>>(body2, obj).Compile();
        }

        public bool Equals(ValueObject<T> x, ValueObject<T> y)
        {
            return this.equalsMethod(x, y);
        }

        public int GetHashCode(ValueObject<T> obj)
        {
            return unchecked(this.hashCodeMethod(obj));
        }

        private static Expression Generate(Type type, Expression left, Expression right)
        {
            var equalityOperator = type.GetEqualityOperatorOrDefault();

            // TODO (Adam): optimise for IList - see http://stackoverflow.com/a/486781/49241
            if (equalityOperator == null && typeof(IEnumerable).IsAssignableFrom(type))
            {
                var genericInterfaces = type.GetInterfaces()
                    .Where(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IEnumerable<>)).ToArray();

                if (genericInterfaces.Any())
                {
                    return genericInterfaces
                        .Select(@interface => Expression.Call(
                            @interface.GetGenericArguments()[0].MakeEnumerableSequenceEqualMethod(),
                            Expression.Convert(left, @interface),
                            Expression.Convert(right, @interface)))
                        .Aggregate((Expression)Expression.Constant(true), (current, expression) => Expression.AndAlso(current, expression));
                }

                return Expression.Equal(Expression.Call(CastToObjects, left), Expression.Call(CastToObjects, right), false, ObjectsEqual);
            }

            return Expression.Equal(left, right, false, equalityOperator);
        }

        private static int GetHashCodeOrDefault<T1>(T1 value)
        {
            return object.Equals(value, default(T1)) ? 0 : value.GetHashCode();
        }
    }
}
