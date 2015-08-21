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
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="events">The events to commit.</param>
        /// <param name="preCommitState">The pre-commit state of the stream.</param>
        /// <param name="postCommitState">The post-commit state of stream.</param>
        void Commit(Guid streamId, IEnumerable<object> events, string preCommitState, out string postCommitState);

        /// <summary>
        /// Gets the events for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="state">The state of the steam.</param>
        /// <returns>The events.</returns>
        IEnumerable<object> Get(Guid streamId, out string state);

        /// <summary>
        /// Gets all the events.
        /// </summary>
        /// <returns>The events.</returns>
        IEnumerable<object> GetAll();

        /// <summary>
        /// Gets all the events of the specified event types.
        /// </summary>
        /// <param name="eventTypes">The event types.</param>
        /// <returns>The events.</returns>
        IEnumerable<object> GetAll(IEnumerable<Type> eventTypes);

        /// <summary>
        /// Gets the events from the specified event identifier.
        /// </summary>
        /// <param name="eventId">The event identifier.</param>
        /// <returns>The events.</returns>
        IEnumerable<object> GetFrom(long eventId);

        /// <summary>
        /// Gets the events from the specified event identifier of the specified event types.
        /// </summary>
        /// <param name="eventId">The event identifier.</param>
        /// <param name="eventTypes">The event types.</param>
        /// <returns>The events.</returns>
        IEnumerable<object> GetFrom(long eventId, IEnumerable<Type> eventTypes);
    }
}
