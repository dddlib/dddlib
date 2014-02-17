// <copyright file="Entity.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Model
{
    using System;

    /// <summary>
    /// Represents an entity.
    /// </summary>
    public class Entity : dddlib.Entity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Entity"/> class.
        /// </summary>
        /// <param name="name">The name of the entity.</param>
        public Entity(string name)
        {
            Guard.Against.Null(() => name);
        }

        /// <summary>
        /// Gets the name of the entity.
        /// </summary>
        /// <value>The name of the entity.</value>
        [NaturalKey]
        public string Name { get; private set; }
    }
}
