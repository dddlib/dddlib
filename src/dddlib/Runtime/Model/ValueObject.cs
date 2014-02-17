// <copyright file="ValueObject.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Model
{
    /// <summary>
    /// Represents a value object.
    /// </summary>
    public class ValueObject : dddlib.Entity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValueObject"/> class.
        /// </summary>
        /// <param name="name">The name of the value object.</param>
        public ValueObject(string name)
        {
            Guard.Against.Null(() => name);
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name of the value object.</value>
        [NaturalKey]
        public string Name { get; private set; }
    }
}
