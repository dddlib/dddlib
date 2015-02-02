// <copyright file="EntityType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using dddlib.Runtime;

    internal class EntityType
    {
        // TODO (Cameron): Mess.
        public EntityType(Type runtimeType, NaturalKeySelector naturalKeySelector, IEqualityComparer<string> naturalKeyStringEqualityComparer, MappingCollection mappings)
        {
            Guard.Against.Null(() => runtimeType);
            Guard.Against.Null(() => mappings);

            if (!runtimeType.InheritsFrom(typeof(Entity)))
            {
                throw new RuntimeException(string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not an entity.", runtimeType));
            }

            if (naturalKeySelector != null && naturalKeySelector.RuntimeType != runtimeType)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The specified natural key selector runtime type of '{0}' does not match the specified runtime type '{1}'.",
                        naturalKeySelector.RuntimeType,
                        runtimeType));
            }

            if (naturalKeySelector == null && naturalKeyStringEqualityComparer != null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The natural key string equality comparer can only be specified in conjunction with a natural key selector.",
                        naturalKeySelector.ReturnType));
            }

            if (naturalKeyStringEqualityComparer != null && naturalKeySelector.ReturnType != typeof(string))
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The specified natural key string equality comparer is incompatible with the natural key selector return type '{0}'.",
                        naturalKeySelector.ReturnType));
            }

            this.NaturalKeySelector = naturalKeySelector ?? NaturalKeySelector.Undefined;

            this.NaturalKeyEqualityComparer = naturalKeyStringEqualityComparer == null 
                ? (IEqualityComparer<object>)EqualityComparer<object>.Default
                : new StringObjectEqualityComparer(naturalKeyStringEqualityComparer);

            this.Mappings = mappings;
        }

        public NaturalKeySelector NaturalKeySelector { get; private set; }

        public IEqualityComparer<object> NaturalKeyEqualityComparer { get; private set; }

        public MappingCollection Mappings { get; set; }

        private class StringObjectEqualityComparer : IEqualityComparer<object>
        {
            private readonly IEqualityComparer<string> stringEqualityComparer;

            public StringObjectEqualityComparer(IEqualityComparer<string> stringEqualityComparer)
            {
                this.stringEqualityComparer = stringEqualityComparer;
            }

            public new bool Equals(object x, object y)
            {
                return this.stringEqualityComparer.Equals((string)x, (string)y);
            }

            public int GetHashCode(object obj)
            {
                return this.stringEqualityComparer.GetHashCode((string)obj);
            }
        }
    }
}
