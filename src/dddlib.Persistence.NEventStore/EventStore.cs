// <copyright file="EventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.NEventStore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using dddlib.Sdk;
    using global::NEventStore;

    /// <summary>
    /// Represents the NEventStore implementation of an event store.
    /// </summary>
    public sealed class EventStore : IEventStore
    {
        private readonly IStoreEvents eventStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventStore"/> class.
        /// </summary>
        /// <param name="eventStore">The NEventStore event storage implementation.</param>
        [CLSCompliant(false)]
        public EventStore(IStoreEvents eventStore)
        {
            Guard.Against.Null(() => eventStore);

            this.eventStore = eventStore;
        }

        /// <summary>
        /// Commits the events to a stream.
        /// </summary>
        /// <param name="id">The stream identifier.</param>
        /// <param name="events">The events to commit.</param>
        /// <param name="preCommitState">The pre-commit state of the stream.</param>
        /// <param name="postCommitState">The post-commit state of stream.</param>
        public void CommitStream(Guid id, IEnumerable<object> events, string preCommitState, out string postCommitState)
        {
            Guard.Against.Null(() => events);

            using (var stream = this.eventStore.OpenStream(id, 0, int.MaxValue))
            {
                if (preCommitState != null && preCommitState != stream.CommitSequence.ToString())
                {
                    throw new Exception("Concurrency?");
                }

                foreach (var @event in events)
                {
                    stream.Add(new EventMessage { Body = @event });
                }

                stream.CommitChanges(Guid.NewGuid());

                postCommitState = stream.CommitSequence.ToString();
            }
        }

        /// <summary>
        /// Gets the events for a stream.
        /// </summary>
        /// <param name="id">The stream identifier.</param>
        /// <param name="state">The state of the steam.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetStream(Guid id, out string state)
        {
            using (var stream = this.eventStore.OpenStream(id, 0, int.MaxValue))
            {
                state = stream.CommitSequence.ToString();
                return stream.CommittedEvents.Select(@event => @event.Body);
            }
        }

        /// <summary>
        /// Replays the events to the specified view(s).
        /// </summary>
        /// <param name="views">The views.</param>
        public void ReplayEventsTo(params object[] views)
        {
            Guard.Against.Null(() => views);

            foreach (var @event in this.eventStore.Advanced.GetFrom()
                .SelectMany(commit => commit.Events)
                .Select(@event => @event.Body))
            {
                foreach (var view in views)
                {
                    // TODO (Cameron): This is not very sensible.
                    new DefaultEventDispatcher(view.GetType(), "Handle", BindingFlags.Instance | BindingFlags.Public).Dispatch(view, @event);
                }
            }
        }
    }
}
