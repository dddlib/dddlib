// <copyright file="NaturalKey.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Use this attribute to decorate a natural key on an aggregate root.
    /// </summary>
    //// LINK (Cameron): http://alabaxblog.info/2009/07/why-sealed-attributes/
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class NaturalKey : Attribute
    {
        /// <summary>
        /// When overridden in a derived class, indicates whether the value of this instance is the default value for the derived class.
        /// </summary>
        /// <returns>Returns <c>true</c> if this instance is the default attribute for the class; otherwise, <c>false</c>.</returns>
        public override bool IsDefaultAttribute()
        {
            return true;
        }

        /// <summary>
        /// Use this attribute to decorate a natural key equality comparer on an aggregate root.
        /// </summary>
        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
        public sealed class EqualityComparer : Attribute
        {
            /// <summary>
            /// When overridden in a derived class, indicates whether the value of this instance is the default value for the derived class.
            /// </summary>
            /// <returns>Returns <c>true</c> if this instance is the default attribute for the class; otherwise, <c>false</c>.</returns>
            public override bool IsDefaultAttribute()
            {
                return true;
            }
        }
    }
}
