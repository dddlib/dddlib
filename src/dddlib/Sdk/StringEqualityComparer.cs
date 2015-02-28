// <copyright file="StringEqualityComparer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System.Collections.Generic;

    // TODO (Cameron): Is this the best way to do this?
    internal class StringEqualityComparer : IEqualityComparer<object>, IEqualityComparer<string>
    {
        private readonly IEqualityComparer<string> innerComparer;

        public StringEqualityComparer(IEqualityComparer<string> innerComparer)
        {
            Guard.Against.Null(() => innerComparer);

            this.innerComparer = innerComparer;
        }

        public bool Equals(string x, string y)
        {
            return this.innerComparer.Equals(x, y);
        }

        public int GetHashCode(string obj)
        {
            return this.innerComparer.GetHashCode(obj);
        }

        bool IEqualityComparer<object>.Equals(object x, object y)
        {
            return this.Equals((string)x, (string)y);
        }

        int IEqualityComparer<object>.GetHashCode(object obj)
        {
            return this.GetHashCode((string)obj);
        }
    }
}
