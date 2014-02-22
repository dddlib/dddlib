// <copyright file="ValueObject.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    /*  TODO (Cameron): 
        Fix exceptions (as before).
        Add expression equality based on public property values.
        Think about how this should work with the TypeDescriptors.  */

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using dddlib.Runtime;

    /// <summary>
    /// Represents a value object.
    /// </summary>
    /// <typeparam name="T">The type of value object.</typeparam>
    //// LINK (Cameron): http://lostechies.com/jimmybogard/2007/06/25/generic-value-object-equality/
    public abstract class ValueObject<T> : IEquatable<T>
        where T : ValueObject<T>
    {
        private static readonly IEqualityComparer<object> DefaultEqualityComparer = new EqualityComparer();

        private readonly IEqualityComparer<object> equalityComparer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueObject{T}"/> class.
        /// </summary>
        protected ValueObject()
            : this(DefaultEqualityComparer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueObject{T}"/> class.
        /// </summary>
        /// <param name="equalityComparer">
        /// The comparer to use for equality comparison of each of the items that together comprise the value of this object.
        /// </param>
        protected ValueObject(IEqualityComparer<object> equalityComparer)
        {
            Guard.Against.Null(() => equalityComparer);

            this.equalityComparer = equalityComparer;
        }

        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible anywhere.")]
        public static bool operator ==(ValueObject<T> first, ValueObject<T> second)
        {
            return object.Equals(first, second);
        }

        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Not visible anywhere.")]
        public static bool operator !=(ValueObject<T> first, ValueObject<T> second)
        {
            return !(first == second);
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>Returns <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public sealed override bool Equals(object obj)
        {
            return this.Equals(obj as T);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public sealed override int GetHashCode()
        {
            int multiplier = 59;
            int hashCode = 17;

            foreach (var value in this.GetValueAndValidate())
            {
                if (value != null)
                {
                    unchecked
                    {
                        hashCode = (hashCode * multiplier) + this.equalityComparer.GetHashCode(value);
                    }
                }
            }

            return hashCode;
        }

        /// <summary>
        /// Determines whether the specified object is equal to this instance.
        /// </summary>
        /// <param name="other">The object to compare with this instance.</param>
        /// <returns>Returns <c>true</c> if the specified object is equal to this instance; otherwise, <c>false</c>.</returns>
        //// LINK (Cameron): http://www.infoq.com/articles/Equality-Overloading-DotNET
        public virtual bool Equals(T other)
        {
            if (object.ReferenceEquals(other, null))
            {
                return false;
            }

            if (object.ReferenceEquals(other, this))
            {
                return true;
            }

            if (other.GetType() != this.GetType())
            {
                // NOTE (Cameron): Type mismatch.
                return false;
            }

            var thisValue = this.GetValueAndValidate();
            var otherValue = other.GetValueAndValidate();

            return otherValue.SequenceEqual(thisValue, this.equalityComparer);
        }

        /// <summary>
        /// Gets each of the items that together comprise the value of this object.
        /// </summary>
        /// <returns>An enumeration of the items that together comprise the value of this object.</returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Inappropriate.")]
        protected abstract IEnumerable<object> GetValue();

        [SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "GetValue", Justification = "Method name.")]
        private IEnumerable<object> GetValueAndValidate()
        {
            var value = this.GetValue();
            if (value == null)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to calculate value equality for '{0}' as the overridden 'GetValue' method implementation is returning null.",
                        typeof(T).FullName));
            }

            return value;
        }

        // TODO (Cameron): Add trace level logging, maybe?
        private class EqualityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y)
            {
                if (object.ReferenceEquals(x, y))
                {
                    return true;
                }

                if (object.ReferenceEquals(x, null) && object.ReferenceEquals(y, null))
                {
                    return true;
                }

                return object.ReferenceEquals(x, null) ? y.Equals(x) : x.Equals(y);
            }

            public virtual int GetHashCode(object obj)
            {
                return obj == null ? 0 : obj.GetHashCode();
            }
        }
    }
}
