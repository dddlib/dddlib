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
        /// Commits the events to a stream.
        /// </summary>
        /// <param name="id">The stream identifier.</param>
        /// <param name="events">The events to commit.</param>
        /// <param name="preCommitState">The pre-commit state of the stream.</param>
        /// <param name="postCommitState">The post-commit state of stream.</param>
        void CommitStream(Guid id, IEnumerable<object> events, string preCommitState, out string postCommitState);

        /// <summary>
        /// Gets the events for a stream.
        /// </summary>
        /// <param name="id">The stream identifier.</param>
        /// <param name="state">The state of the steam.</param>
        /// <returns>The events.</returns>
        IEnumerable<object> GetStream(Guid id, out string state);

        /// <summary>
        /// Replays the events to the specified view(s).
        /// </summary>
        /// <param name="views">The views.</param>
        void ReplayEventsTo(params object[] views);
    }
}
