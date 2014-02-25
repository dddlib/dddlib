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
        /// The event sourcing runtime mode.
        /// When this mode is specified then the <see cref="dddlib.AggregateRoot"/> type will respect event dispatching.
        /// This is the default mode.
        /// </summary>
        EventSourcing = 0,

        /// <summary>
        /// The event sourcing runtime mode without persistence.
        /// This is the same as the <see cref="dddlib.RuntimeMode.EventSourcing"/> runtime mode except that events will not be stored on the 
        /// aggregate root for persistence.
        /// </summary>
        EventSourcingWithoutPersistence = 1,

        /// <summary>
        /// The plain runtime mode.
        /// In this mode event dispatching will not occur and consequently events will not be stored on the aggregate root for persistence.
        /// </summary>
        Plain = 2,
    }
}
