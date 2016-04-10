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
        /// <param name="sequenceNumber">The sequence number for the event.</param>
        public EventCommittedEventArgs(long sequenceNumber)
        {
            this.SequenceNumber = sequenceNumber;
        }

        /// <summary>
        /// Gets the sequence number for the event.
        /// </summary>
        /// <value>The sequence number for the event.</value>
        public long SequenceNumber { get; private set; }
    }
}
