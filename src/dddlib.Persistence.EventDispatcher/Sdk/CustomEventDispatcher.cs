// <copyright file="CustomEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Sdk
{
    using System;

    /// <summary>
    /// Represents a custom event dispatcher.
    /// </summary>
    public class CustomEventDispatcher : IEventDispatcher
    {
        private readonly Action<long, object> eventDispatcherDelegate;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomEventDispatcher"/> class.
        /// </summary>
        /// <param name="eventDispatcherDelegate">The event dispatcher delegate.</param>
        public CustomEventDispatcher(Action<long, object> eventDispatcherDelegate)
        {
            Guard.Against.Null(() => eventDispatcherDelegate);

            this.eventDispatcherDelegate = eventDispatcherDelegate;
        }

        /// <summary>
        /// Dispatches the specified event.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number for the event.</param>
        /// <param name="event">The event.</param>
        public void Dispatch(long sequenceNumber, object @event)
        {
            this.eventDispatcherDelegate.Invoke(sequenceNumber, @event);
        }
    }
}
