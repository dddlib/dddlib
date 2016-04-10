// <copyright file="IEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher
{
    /// <summary>
    /// Exposes the public members of the event dispatcher.
    /// </summary>
    public interface IEventDispatcher
    {
        /// <summary>
        /// Dispatches the specified event.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number for the event.</param>
        /// <param name="event">The event.</param>
        void Dispatch(long sequenceNumber, object @event);
    }
}
