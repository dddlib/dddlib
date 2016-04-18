// <copyright file="MemoryEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Memory
{
    using System;
    using System.Collections.Generic;
    using dddlib.Persistence.EventDispatcher.Sdk;

    internal class MemoryEventStore : IEventStore
    {
        private readonly Dictionary<Guid, List<Event>> eventStreams = new Dictionary<Guid, List<Event>>();

        public Batch GetNextUndispatchedEventsBatch(int batchSize)
        {
            throw new NotImplementedException();
        }

        public void MarkEventAsDispatched(long sequenceNumber)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetEventsFrom(long sequenceNumber)
        {
            throw new NotImplementedException();
        }
    }
}
