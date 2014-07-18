// <copyright file="IEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Exposes the public members of the event store.
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// Commits the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="events">The events.</param>
        /// <param name="state">The state.</param>
        /// <param name="newState">The new state.</param>
        void CommitStream(Guid id, IEnumerable<object> events, string state, out string newState);

        /// <summary>
        /// Gets the stream.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="state">The state.</param>
        /// <returns>The events.</returns>
        IEnumerable<object> GetStream(Guid id, out string state);

        /// <summary>
        /// Replays the events to.
        /// </summary>
        /// <param name="views">The views.</param>
        void ReplayEventsTo(params object[] views);
    }
}
