// <copyright file="ValueObjectType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    // TODO (Cameron): Make equality comparer a concrete class or something like that.
    internal class ValueObjectType
    {
        private readonly object equalityComparer;

        public ValueObjectType(Type runtimeType, object equalityComparer)
        {
            Guard.Against.Null(() => runtimeType);

            if (!IsSubclassOfRawGeneric(typeof(ValueObject<>), runtimeType))
            {
                throw new RuntimeException(
                    string.Format(CultureInfo.InvariantCulture, "The specified type '{0}' is not a value object.", runtimeType));
            }

            this.equalityComparer = equalityComparer;
        }

        public IEqualityComparer<T> CreateEqualityComparer<T>()
            where T : ValueObject<T>
        {
            return (this.equalityComparer as IEqualityComparer<T>) ?? new ValueObjectEqualityComparer<T>();
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
    }
}
