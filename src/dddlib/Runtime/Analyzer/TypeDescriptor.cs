// <copyright file="TypeDescriptor.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Analyzer
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Describes a type.
    /// </summary>
    public class TypeDescriptor : Descriptor // Entity
    {
        internal TypeDescriptor()
        {
        }

        /// <summary>
        /// Gets a value indicating whether [is aggregate root].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [is aggregate root]; otherwise, <c>false</c>.
        /// </value>
        public bool IsAggregateRoot { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether [is entity].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [is entity]; otherwise, <c>false</c>.
        /// </value>
        public bool IsEntity { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether [is value object].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [is value object]; otherwise, <c>false</c>.
        /// </value>
        public bool IsValueObject { get; internal set; }

        /// <summary>
        /// Gets the event dispatcher.
        /// </summary>
        /// <value>
        /// The event dispatcher.
        /// </value>
        public IEventDispatcher EventDispatcher { get; internal set; }

        /// <summary>
        /// Gets the equality comparer.
        /// </summary>
        /// <value>
        /// The equality comparer.
        /// </value>
        public IEqualityComparer<object> EqualityComparer { get; internal set; }

        /// <summary>
        /// Gets the factory.
        /// </summary>
        /// <value>
        /// The factory.
        /// </value>
        public Func<object> Factory { get; internal set; }
    }
}
