// <copyright file="ValueObjectConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    /// <summary>
    /// Represents the value object configuration.
    /// </summary>
    public class ValueObjectConfiguration
    {
        /// <summary>
        /// Gets or sets the equality comparer.
        /// </summary>
        /// <value>The equality comparer.</value>
        public object EqualityComparer { get; set; }
    }
}
