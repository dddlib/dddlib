// <copyright file="EventCommittedEventArgs.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Sdk
{
    using System;

    /// <summary>
    /// Represents the set of arguments passed to the event committed event handler.
    /// </summary>
    public class EventCommittedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventCommittedEventArgs"/> class.
        /// </summary>
        /// <param name="eventId">The event identifier.</param>
        public EventCommittedEventArgs(long eventId)
        {
            this.EventId = eventId;
        }

        /// <summary>
        /// Gets the event identifier.
        /// </summary>
        /// <value>The event identifier.</value>
        public long EventId { get; private set; }
    }
}
