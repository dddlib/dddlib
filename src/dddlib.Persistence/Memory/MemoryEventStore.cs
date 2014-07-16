// <copyright file="MemoryEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Represents a memory-based event store.
    /// </summary>
    public class MemoryEventStore : IEventStore
    {
        private readonly Dictionary<Guid, Data> store = new Dictionary<Guid, Data>();

        /// <summary>
        /// Commits the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="events">The events.</param>
        /// <param name="state">The state.</param>
        /// <param name="newState">The new state.</param>
        public void CommitStream(Guid id, IEnumerable<object> events, string state, out string newState)
        {
            var data = default(Data);
            if (this.store.TryGetValue(id, out data))
            {
                if (data.State != state)
                {
                    throw new Exception("Invalid state");
                }
            }
            else if (state != null)
            {
                // TODO (Cameron): Not sure if this should be here...
                throw new Exception("Invalid state #2");
            }

            data = data ?? new Data();

            data.Events = data.Events ?? new List<object>();
            data.Events.AddRange(events);
            data.State = newState = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

            this.store[id] = data;
        }

        /// <summary>
        /// Gets the stream.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="state">The state.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetStream(Guid id, out string state)
        {
            var data = default(Data);
            if (!this.store.TryGetValue(id, out data))
            {
                throw new Exception("Invalid state #2");
            }

            state = data.State;

            return data.Events.ToArray();
        }

        private class Data
        {
            public List<object> Events { get; set; }

            public string State { get; set; }
        }
    }
}
