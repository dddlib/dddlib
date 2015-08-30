// <copyright file="IEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Sdk
{
    /// <summary>
    /// Exposes the public members of the event store (for the event dispatcher).
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// Gets the next undispatched events batch.
        /// </summary>
        /// <param name="batchSize">Size of the batch.</param>
        /// <returns>The events batch.</returns>
        Batch GetNextUndispatchedEventsBatch(int batchSize);

        /// <summary>
        /// Marks the event as dispatched.
        /// </summary>
        /// <param name="eventId">The event identifier.</param>
        void MarkEventAsDispatched(long eventId);
    }
}
