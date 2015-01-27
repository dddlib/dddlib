// <copyright file="ValueObjectType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using dddlib.Runtime;

    internal class ValueObjectType
    {
        public ValueObjectType(Type runtimeType, object equalityComparer, MappingCollection mappings)
        {
            Guard.Against.Null(() => runtimeType);
            Guard.Against.Null(() => equalityComparer);
            Guard.Against.Null(() => mappings);

            if (!runtimeType.InheritsFrom(typeof(ValueObject<>)))
            {
                throw new RuntimeException(
                    string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not a value object.", runtimeType));
            }

            var equalityComparerType = typeof(IEqualityComparer<>).MakeGenericType(runtimeType);
            if (!equalityComparerType.IsAssignableFrom(equalityComparer.GetType()))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid equality comparer. The specified equality comparer of type '{0}' does not match the required type of '{1}'.",
                        equalityComparer.GetType(),
                        equalityComparerType));
            }

            this.EqualityComparer = equalityComparer;
            this.Mappings = mappings;
        }

        public object EqualityComparer { get; private set; }

        public MappingCollection Mappings { get; set; }
    }
}
