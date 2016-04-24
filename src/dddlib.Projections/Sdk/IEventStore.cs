// <copyright file="IEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Projections.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Exposes the public members of the event store (for the event dispatcher).
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// Gets the events from the specified sequence number.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number.</param>
        /// <returns>The events.</returns>
        IEnumerable<object> GetEventsFrom(long sequenceNumber);
    }
}
