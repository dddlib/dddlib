// <copyright file="Descriptor.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Analyzer
{
    using System;

    /// <summary>
    /// The base class for all descriptors.
    /// </summary>
    public abstract class Descriptor
    {
        /// <summary>
        /// Adds the specified description.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <param name="arguments">The arguments.</param>
        public void Add(string description, params string[] arguments)
        {
        }

        /// <summary>
        /// Adds the specified exception.
        /// </summary>
        /// <param name="ex">The exception.</param>
        /// <param name="description">The description.</param>
        /// <param name="arguments">The arguments.</param>
        public void Add(Exception ex, string description, params string[] arguments)
        {
        }
    }
}
