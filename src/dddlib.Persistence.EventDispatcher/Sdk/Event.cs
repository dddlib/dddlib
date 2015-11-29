// <copyright file="Event.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Sdk
{
    /// <summary>
    /// Represents an event.
    /// </summary>
    public class Event
    {
        /// <summary>
        /// Gets or sets the event identifier.
        /// </summary>
        /// <value>The event identifier.</value>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the event payload.
        /// </summary>
        /// <value>The event payload.</value>
        public object Payload { get; set; }
    }
}
