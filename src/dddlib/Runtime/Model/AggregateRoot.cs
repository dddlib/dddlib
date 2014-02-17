// <copyright file="AggregateRoot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Model
{
    /// <summary>
    /// Represents an aggregate root.
    /// </summary>
    public class AggregateRoot : Entity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRoot"/> class.
        /// </summary>
        /// <param name="name">The name of the aggregate root.</param>
        public AggregateRoot(string name)
            : base(name)
        {
        }
    }
}
