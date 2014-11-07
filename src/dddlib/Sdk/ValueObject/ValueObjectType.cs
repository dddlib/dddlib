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
        public ValueObjectType(Type runtimeType, object equalityComparer)
        {
            Guard.Against.Null(() => runtimeType);
            Guard.Against.Null(() => equalityComparer);

            if (!runtimeType.InheritsFrom(typeof(ValueObject<>)))
            {
                throw new RuntimeException(
                    string.Format(CultureInfo.InvariantCulture, "The specified type '{0}' is not a value object.", runtimeType));
            }

            var equalityComparerType = typeof(IEqualityComparer<>).MakeGenericType(runtimeType);
            if (equalityComparer != null && equalityComparer.GetType().IsAssignableFrom(equalityComparerType))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The specified equality comparer type of '{0}' does not match the required type of '{1}'.",
                        equalityComparer.GetType(),
                        equalityComparerType));
            }

            this.EqualityComparer = equalityComparer;
        }

        public object EqualityComparer { get; private set; }
    }
}
