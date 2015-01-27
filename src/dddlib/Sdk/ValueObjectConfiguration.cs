// <copyright file="ValueObjectConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;

    /// <summary>
    /// Represents the value object configuration.
    /// </summary>
    public class ValueObjectConfiguration
    {
        /// <summary>
        /// Gets or sets the runtime type of the value object.
        /// </summary>
        /// <value>The runtime type of the value object.</value>
        public Type RuntimeType { get; set; }

        /// <summary>
        /// Gets or sets the equality comparer.
        /// </summary>
        /// <value>The equality comparer.</value>
        //// TODO (Cameron): Make concrete type - not object.
        public object EqualityComparer { get; set; }

        /// <summary>
        /// Gets or sets the mappings for this value object.
        /// </summary>
        /// <value>The mappings for this value object.</value>
        public MappingCollection Mappings { get; set; }
    }
}
