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
        private readonly Dictionary<Guid, List<Event>> eventStreams = new Dictionary<Guid, List<Event>>();
        private readonly Dictionary<Guid, Dictionary<int, object>> snapshots = new Dictionary<Guid, Dictionary<int, object>>();

        private long currentEventId;

        /// <summary>
        /// Commits the events to a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="events">The events to commit.</param>
        /// <param name="preCommitState">The pre-commit state of the stream.</param>
        /// <param name="postCommitState">The post-commit state of stream.</param>
        public void CommitStream(Guid streamId, IEnumerable<object> events, string preCommitState, out string postCommitState)
        {
            var eventStream = default(List<Event>);
            if (this.eventStreams.TryGetValue(streamId, out eventStream))
            {
                if (eventStream.Last().State != preCommitState)
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
                eventStream = new List<Event>();
                this.eventStreams.Add(streamId, eventStream);
            }

            var commitTimestamp = DateTime.UtcNow;

            eventStream.AddRange(
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

            postCommitState = eventStream.Last().State;
        }

        /// <summary>
        /// Gets the events for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="streamRevision">The stream revision to get the events from.</param>
        /// <param name="state">The state of the steam.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetStream(Guid streamId, int streamRevision, out string state)
        {
            Guard.Against.Negative(() => streamRevision);

            var eventStream = default(List<Event>);
            if (!this.eventStreams.TryGetValue(streamId, out eventStream))
            {
                throw new Exception("Invalid state #2");
            }

            state = eventStream.Last().State;

            return eventStream.Skip(streamRevision + 1).Select(@event => @event.Payload).ToList();
        }

        /// <summary>
        /// Adds a snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="streamRevision">The stream revision.</param>
        /// <param name="memento">The memento for the snapshot.</param>
        public void AddSnapshot(Guid streamId, int streamRevision, object memento)
        {
            Guard.Against.Null(() => memento);

            var streamSnapshots = default(Dictionary<int, object>);
            if (!this.snapshots.TryGetValue(streamId, out streamSnapshots))
            {
                streamSnapshots = new Dictionary<int, object>();
                this.snapshots.Add(streamId, streamSnapshots);
            }

            streamSnapshots.Add(streamRevision, memento);
        }

        /// <summary>
        /// Gets the latest snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="streamRevision">The stream revision.</param>
        /// <returns>The memento for the snapshot.</returns>
        public object GetSnapshot(Guid streamId, out int streamRevision)
        {
            var streamSnapshots = default(Dictionary<int, object>);
            if (!this.snapshots.TryGetValue(streamId, out streamSnapshots))
            {
                // NOTE (Cameron): There are no saved snapshots for this stream.
                streamRevision = 0;
                return null;
            }

            streamRevision = streamSnapshots.Max(snapshot => snapshot.Key);

            return streamSnapshots[streamRevision];
        }

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
