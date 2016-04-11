// <copyright file="IEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Sdk
{
    using System.Collections.Generic;

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
        /// <param name="sequenceNumber">The sequence number for the event.</param>
        void MarkEventAsDispatched(long sequenceNumber);

        /// <summary>
        /// Gets the events from the specified sequence number.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number.</param>
        /// <returns>The events.</returns>
        IEnumerable<object> GetEventsFrom(long sequenceNumber);
    }
}
