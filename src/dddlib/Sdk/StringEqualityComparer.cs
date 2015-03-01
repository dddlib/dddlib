// <copyright file="StringEqualityComparer.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Defines methods to support the comparison of strings as objects for equality.
    /// </summary>
    //// TODO (Cameron): Consider whether this is the best way to do this?
    public sealed class StringEqualityComparer : IEqualityComparer<object>, IEqualityComparer<string>
    {
        private readonly IEqualityComparer<string> innerComparer;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEqualityComparer"/> class.
        /// </summary>
        /// <param name="innerComparer">The inner comparer.</param>
        public StringEqualityComparer(IEqualityComparer<string> innerComparer)
        {
            Guard.Against.Null(() => innerComparer);

            this.innerComparer = innerComparer;
        }

        /// <summary>
        /// Determines whether the specified strings are equal.
        /// </summary>
        /// <param name="x">The first string to compare.</param>
        /// <param name="y">The second string to compare.</param>
        /// <returns>Returns <c>true</c> if the specified objects are equal; otherwise, <c>false</c>.</returns>
        public bool Equals(string x, string y)
        {
            return this.innerComparer.Equals(x, y);
        }

        /// <summary>
        /// Returns a hash code for the specified string.
        /// </summary>
        /// <param name="obj">The string for which a hash code is to be returned.</param>
        /// <returns>A hash code for this string, suitable for use in hashing algorithms and data structures like a hash table. </returns>
        public int GetHashCode(string obj)
        {
            return this.innerComparer.GetHashCode(obj);
        }

        bool IEqualityComparer<object>.Equals(object x, object y)
        {
            return this.Equals(x as string, y as string);
        }

        int IEqualityComparer<object>.GetHashCode(object obj)
        {
            return this.GetHashCode(obj as string);
        }
    }
}
