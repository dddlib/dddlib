// <copyright file="EntityType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using dddlib.Sdk;

    internal class EntityType
    {
        // TODO (Cameron): Mess.
        public EntityType(NaturalKeySelector naturalKeySelector, IEqualityComparer<string> naturalKeyStringEqualityComparer)
        {
            this.NaturalKeySelector = naturalKeySelector;
            this.NaturalKeyEqualityComparer = naturalKeyStringEqualityComparer == null 
                ? (IEqualityComparer<object>)EqualityComparer<object>.Default
                : new StringObjectEqualityComparer(naturalKeyStringEqualityComparer);
        }

        public NaturalKeySelector NaturalKeySelector { get; private set; }

        public IEqualityComparer<object> NaturalKeyEqualityComparer { get; private set; }

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
                return this.GetHashCode((string)obj);
            }
        }
    }
}
