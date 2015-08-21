// <copyright file="MemoryEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using dddlib.Sdk;

    /// <summary>
    /// Represents a memory-based event store.
    /// </summary>
    public class MemoryEventStore : IEventStore
    {
        private readonly Dictionary<Guid, List<Event>> eventStore = new Dictionary<Guid, List<Event>>();

        private long currentEventId;

        /// <summary>
        /// Commits the events to a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="events">The events to commit.</param>
        /// <param name="preCommitState">The pre-commit state of the stream.</param>
        /// <param name="postCommitState">The post-commit state of stream.</param>
        public void Commit(Guid streamId, IEnumerable<object> events, string preCommitState, out string postCommitState)
        {
            var comittedEvents = default(List<Event>);
            if (this.eventStore.TryGetValue(streamId, out comittedEvents))
            {
                if (comittedEvents.Last().State != preCommitState)
                {
                    throw new Exception("Invalid state");
                }
            }
            else if (preCommitState != null)
            {
                // TODO (Cameron): Not sure if this should be here...
                throw new Exception("Invalid state #2");
            }
            else
            {
                comittedEvents = new List<Event>();
                this.eventStore.Add(streamId, comittedEvents);
            }

            var commitTimestamp = DateTime.UtcNow;

            comittedEvents.AddRange(
                events.Select(
                    @event => 
                    new Event
                    {
                        Id = ++this.currentEventId,
                        Type = @event.GetType(),
                        Payload = @event,
                        State = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                        CommitTimestamp = commitTimestamp,
                    }));

            postCommitState = comittedEvents.Last().State;
        }

        /// <summary>
        /// Gets the events for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="state">The state of the steam.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> Get(Guid streamId, out string state)
        {
            var comittedEvents = default(List<Event>);
            if (!this.eventStore.TryGetValue(streamId, out comittedEvents))
            {
                throw new Exception("Invalid state #2");
            }

            state = comittedEvents.Last().State;

            return comittedEvents.Select(@event => @event.Payload).ToList();
        }

        /// <summary>
        /// Gets all the events.
        /// </summary>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetAll()
        {
            return this.eventStore.Values
                .SelectMany(comittedEvents => comittedEvents)
                .OrderBy(@event => @event.Id)
                .Select(@event => @event.Payload)
                .ToList();
        }

        /// <summary>
        /// Gets all the events of the specified event types.
        /// </summary>
        /// <param name="eventTypes">The event types.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetAll(IEnumerable<Type> eventTypes)
        {
            Guard.Against.NullOrEmptyOrNullElements(() => eventTypes);

            return this.eventStore.Values
                .SelectMany(comittedEvents => comittedEvents)
                .Where(@event => eventTypes.Contains(@event.Type))
                .OrderBy(@event => @event.Id)
                .Select(@event => @event.Payload)
                .ToList();
        }

        /// <summary>
        /// Gets the events from the specified event identifier.
        /// </summary>
        /// <param name="eventId">The event identifier.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetFrom(long eventId)
        {
            return this.eventStore.Values
                .SelectMany(comittedEvents => comittedEvents)
                .Where(@event => @event.Id >= eventId)
                .OrderBy(@event => @event.Id)
                .Select(@event => @event.Payload)
                .ToList();
        }

        /// <summary>
        /// Gets the events from the specified event identifier of the specified event types.
        /// </summary>
        /// <param name="eventId">The event identifier.</param>
        /// <param name="eventTypes">The event types.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetFrom(long eventId, IEnumerable<Type> eventTypes)
        {
            Guard.Against.NullOrEmptyOrNullElements(() => eventTypes);

            return this.eventStore.Values
                .SelectMany(comittedEvents => comittedEvents)
                .Where(@event => eventTypes.Contains(@event.Type))
                .Where(@event => @event.Id >= eventId)
                .OrderBy(@event => @event.Id)
                .Select(@event => @event.Payload)
                .ToList();
        }

        ////public void ReplayEventsTo(params object[] views)
        ////{
        ////    foreach (var view in views)
        ////    {
        ////        foreach (var @event in this.store
        ////            .OrderBy(e => e.Value.Timestamp)
        ////            .SelectMany(e => e.Value.Events))
        ////        {
        ////            // TODO (Cameron): This is not very sensible.
        ////            new DefaultEventDispatcher(view.GetType(), "Handle", BindingFlags.Instance | BindingFlags.Public).Dispatch(view, @event);
        ////        }
        ////    }
        ////}

        private class Event
        {
            public long Id { get; set; }

            public Type Type { get; set; }

            public object Payload { get; set; }

            public string State { get; set; }

            public DateTime CommitTimestamp { get; set; }
        }
    }
}
