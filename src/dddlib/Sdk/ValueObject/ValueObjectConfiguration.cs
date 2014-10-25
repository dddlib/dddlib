// <copyright file="ValueObjectConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;

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

        ////public Action<object, object> ToEventMapping { get; set; }

        ////public Func<object, object> FromEventMapping { get; set; }
    }
}
