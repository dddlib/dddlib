// <copyright file="RuntimeMode.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    /// <summary>
    /// Describes the runtime mode.
    /// </summary>
    public enum RuntimeMode
    {
        /// <summary>
        /// The plain runtime mode.
        /// </summary>
        Plain = 1,

        /// <summary>
        /// The event sourcing runtime mode.
        /// </summary>
        EventSourcing = 2,

        /// <summary>
        /// The event sourcing without persistence runtime mode.
        /// This is the same as the <see cref="RuntimeMode.EventSourcing"/> runtime mode except that events will not be stored on the aggregate root
        /// for persistence.
        /// </summary>
        EventSourcingWithoutPersistence = 4,
    }
}
