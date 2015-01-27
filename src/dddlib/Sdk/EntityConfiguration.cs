// <copyright file="EntityConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the entity configuration.
    /// </summary>
    public class EntityConfiguration
    {
        /// <summary>
        /// Gets or sets the runtime type of the entity.
        /// </summary>
        /// <value>The runtime type of the entity.</value>
        public Type RuntimeType { get; set; }

        /// <summary>
        /// Gets or sets the name of the natural key property.
        /// </summary>
        /// <value>The name of the natural key property.</value>
        public string NaturalKeyPropertyName { get; set; }

        /// <summary>
        /// Gets or sets the natural key string equality comparer.
        /// </summary>
        /// <value>The natural key string equality comparer.</value>
        public IEqualityComparer<string> NaturalKeyStringEqualityComparer { get; set; }
    }
}
