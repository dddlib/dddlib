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
    using System.Diagnostics.CodeAnalysis;
    using dddlib.Runtime;
    using dddlib.Sdk.Configuration.Model;

    /// <summary>
    /// Represents a value object.
    /// </summary>
    /// <typeparam name="T">The type of value object.</typeparam>
    public abstract partial class ValueObject<T> : IEquatable<T>
        where T : ValueObject<T>
    {
        // PERF (Cameron): Introduced to reduce allocations of the function delegates for type information.
        private static readonly Func<ValueObject<T>, TypeInformation> GetTypeInformation =
            @this => new TypeInformation(Application.Current.GetValueObjectType(@this.GetType()));

        private readonly TypeInformation typeInformation;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueObject{T}"/> class.
        /// </summary>
        protected ValueObject()
            : this(GetTypeInformation)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueObject{T}"/> class.
        /// </summary>
        /// <param name="valueObjectType">The value object type.</param>
        protected ValueObject(ValueObjectType valueObjectType)
            : this(@this => new TypeInformation(valueObjectType))
        {
        }

        // LINK (Cameron): http://stackoverflow.com/questions/2287636/pass-current-object-type-into-base-constructor-call
        private ValueObject(Func<ValueObject<T>, TypeInformation> getTypeInformation)
        {
            this.typeInformation = getTypeInformation(this);
        }

#pragma warning disable 1591
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
#pragma warning restore 1591

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
            return this.typeInformation.EqualityComparer.GetHashCode(this as T);
        }

        /// <summary>
        /// Determines whether the specified object is equal to this instance.
        /// </summary>
        /// <param name="other">The object to compare with this instance.</param>
        /// <returns>Returns <c>true</c> if the specified object is equal to this instance; otherwise, <c>false</c>.</returns>
        //// LINK (Cameron): http://www.infoq.com/articles/Equality-Overloading-DotNET
        public bool Equals(T other)
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

            return this.typeInformation.EqualityComparer.Equals(this as T, other);
        }
    }
}
