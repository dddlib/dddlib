// <copyright file="TypeExtensions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    internal static class TypeExtensions
    {
        public static bool IsNullableType(this Type type)
        {
            Guard.Against.Null(() => type);

            return !type.IsValueType || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        public static MethodInfo MakeEnumerableCastMethod(this Type type)
        {
            return typeof(EnumerableProxy).GetMethod("Cast").MakeGenericMethod(type);
        }

        public static MethodInfo MakeEnumerableSequenceEqualMethod(this Type type)
        {
            return typeof(EnumerableProxy).GetMethod("SequenceEqual").MakeGenericMethod(type);
        }

        public static MethodInfo GetEqualityOperatorOrDefault(this Type type)
        {
            Guard.Against.Null(() => type);

            for (; type != typeof(object); type = type.BaseType)
            {
                var method = GetDeclaredEqualityOperatorOrDefault(type);
                if (method != null)
                {
                    return method;
                }
            }

            return typeof(object).GetDeclaredEqualityOperatorOrDefault();
        }

        public static bool InheritsFrom(this Type type, Type subClass)
        {
            return subClass.IsGenericTypeDefinition
                ? IsSubclassOfRawGeneric(subClass, type)
                : subClass.IsAssignableFrom(type);
        }

        private static MethodInfo GetDeclaredEqualityOperatorOrDefault(this Type type)
        {
            return type.GetMethods()
                .Where(candidate => candidate.Name == "op_Equality")
                .FirstOrDefault(candidate =>
                {
                    var parameters = candidate.GetParameters();
                    return parameters.Length == 2 && parameters[0].ParameterType == type && parameters[1].ParameterType == type;
                });
        }

        private static bool IsSubclassOfRawGeneric(Type genericType, Type type)
        {
            while (type != null && type != typeof(object))
            {
                var currentType = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (genericType == currentType)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static class EnumerableProxy
        {
            public static IEnumerable<TResult> Cast<TResult>(IEnumerable source)
            {
                return source.Cast<TResult>();
            }

            public static bool SequenceEqual<TSource>(IEnumerable<TSource> first, IEnumerable<TSource> second)
            {
                return first.SequenceEqual(second);
            }
        }
    }
}