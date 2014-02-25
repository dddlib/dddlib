// <copyright file="NaturalKeyAttribute.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Represents the attribute used to identify a natural key on an aggregate root.
    /// </summary>
    //// LINK (Cameron): http://alabaxblog.info/2009/07/why-sealed-attributes/
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class NaturalKeyAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the equality comparer for the natural key of the entity.
        /// </summary>
        /// <value>The equality comparer for the natural key of the entity.</value>
        public Type EqualityComparer { get; set; }

        /// <summary>
        /// When overridden in a derived class, indicates whether the value of this instance is the default value for the derived class.
        /// </summary>
        /// <returns>Returns <c>true</c> if this instance is the default attribute for the class; otherwise, <c>false</c>.</returns>
        public override bool IsDefaultAttribute()
        {
            return this.EqualityComparer == null;
        }
    }
}
