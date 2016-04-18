// <copyright file="MemoryEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using System.Collections.Generic;
    using System.IO.MemoryMappedFiles;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Web.Script.Serialization;
    using dddlib.Sdk;
    using Sdk;

    /// <summary>
    /// Represents the memory event store.
    /// </summary>
    public sealed class MemoryEventStore : IEventStore, IDisposable
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly Dictionary<Guid, List<Event>> eventStreams = new Dictionary<Guid, List<Event>>();
        private readonly List<Event> store = new List<Event>();

        private readonly Mutex mutex;
        private readonly MemoryMappedFile file;

        private long readOffset;
        private long writeOffset;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryEventStore"/> class.
        /// </summary>
        public MemoryEventStore()
        {
            var securitySettings = new MutexSecurity();
            securitySettings.AddAccessRule(
                new MutexAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl,
                AccessControlType.Allow));

            var mutexCreated = false;
            this.mutex = new Mutex(false, @"Global\MemoryEventStore", out mutexCreated, securitySettings);
            this.file = MemoryMappedFile.CreateOrOpen("MemoryEventStore", 10 * 1024 * 1024 /* 10MB */);

            Serializer.RegisterConverters(new[] { new DateTimeConverter() });
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

            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.Synchronize();

            var eventStream = default(List<Event>);
            if (!this.eventStreams.TryGetValue(streamId, out eventStream))
            {
                state = null;
                return new object[0];
            }

            state = eventStream.Last().State;

            return eventStream.Skip(streamRevision).Select(@event => @event.Payload).ToList();
        }

        /// <summary>
        /// Commits the events to a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="events">The events to commit.</param>
        /// <param name="correlationId">The correlation identifier.</param>
        /// <param name="preCommitState">The pre-commit state of the stream.</param>
        /// <param name="postCommitState">The post-commit state of stream.</param>
        public void CommitStream(Guid streamId, IEnumerable<object> events, Guid correlationId, string preCommitState, out string postCommitState)
        {
            Guard.Against.Null(() => events);

            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            using (new ExclusiveCodeBlock(this.mutex))
            {
                this.Synchronize();

                postCommitState = null;

                var eventStream = default(List<Event>);
                if (this.eventStreams.TryGetValue(streamId, out eventStream))
                {
                    if (eventStream.Last().State != preCommitState)
                    {
                        throw preCommitState == null
                            ? new ConcurrencyException("Aggregate root already exists.")
                            : new ConcurrencyException();
                    }

                    // NOTE (Cameron): Only if there are no events to commit.
                    postCommitState = eventStream.Last().State;
                }

                if (!events.Any())
                {
                    return;
                }

                foreach (var @event in events)
                {
                    var payload = Serializer.Serialize(@event);
                    var memoryMappedEvent = new MemoryMappedEvent
                    {
                        StreamId = streamId,
                        Type = @event.GetType().GetSerializedName(),
                        Payload = payload,
                        SequenceNumber = this.store.Count + 1,
                        State = postCommitState = Guid.NewGuid().ToString("N").Substring(0, 8),
                    };

                    var buffer = Encoding.UTF8.GetBytes(Serializer.Serialize(memoryMappedEvent));

                    using (var accessor = this.file.CreateViewAccessor(this.writeOffset, 2 + buffer.Length))
                    {
                        accessor.Write(0, (ushort)buffer.Length);
                        accessor.WriteArray(2, buffer, 0, buffer.Length);
                    }

                    this.writeOffset += 2 + buffer.Length;
                }
            }
        }

        /// <summary>
        /// Gets the events from the specified sequence number.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetEventsFrom(long sequenceNumber)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.Synchronize();

            return this.store.Skip((int)sequenceNumber).Select(@event => @event.Payload);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.mutex.Dispose();
            this.file.Dispose();

            this.isDisposed = true;
        }

        private void Synchronize()
        {
            var length = 0;
            do
            {
                using (var accessor = this.file.CreateViewAccessor(this.readOffset, 2))
                {
                    length = accessor.ReadUInt16(0);
                    if (length == 0)
                    {
                        break;
                    }
                }

                var buffer = new byte[length];
                using (var accessor = this.file.CreateViewAccessor(this.readOffset + 2, length))
                {
                    accessor.ReadArray(0, buffer, 0, length);
                }

                var serializedEvent = Encoding.UTF8.GetString(buffer);
                var memoryMappedEvent = Serializer.Deserialize<MemoryMappedEvent>(serializedEvent);
                var @event = new Event
                {
                    SequenceNumber = memoryMappedEvent.SequenceNumber,
                    Payload = Serializer.Deserialize(memoryMappedEvent.Payload, Type.GetType(memoryMappedEvent.Type)),
                    State = memoryMappedEvent.State,
                };

                this.readOffset += 2 + buffer.Length;

                var eventStream = default(List<Event>);
                if (!this.eventStreams.TryGetValue(memoryMappedEvent.StreamId, out eventStream))
                {
                    eventStream = new List<Event>();
                    this.eventStreams.Add(memoryMappedEvent.StreamId, eventStream);
                }

                eventStream.Add(@event);
                this.store.Add(@event);
            }
            while (length > 0);

            this.writeOffset = this.readOffset;
        }

        private class Event
        {
            public long SequenceNumber { get; set; }

            public object Payload { get; set; }

            public string State { get; set; }
        }

        private class MemoryMappedEvent
        {
            public Guid StreamId { get; set; }

            public string Type { get; set; }

            public string Payload { get; set; }

            public long SequenceNumber { get; set; }

            public string State { get; set; }
        }
    }
}
