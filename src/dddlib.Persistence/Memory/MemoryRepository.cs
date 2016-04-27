// <copyright file="MemoryRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using dddlib.Persistence.Sdk;

    /// <summary>
    /// Represents a memory-based repository.
    /// </summary>
    /// <typeparam name="T">The type of aggregate root.</typeparam>
    public class MemoryRepository<T> : Repository<T> where T : AggregateRoot
    {
        private readonly Dictionary<Guid, Data> store = new Dictionary<Guid, Data>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryRepository{T}"/> class.
        /// </summary>
        public MemoryRepository()
            : this(new MemoryIdentityMap())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryRepository{T}"/> class.
        /// </summary>
        /// <param name="identityMap">The identity map.</param>
        public MemoryRepository(IIdentityMap identityMap)
            : base(identityMap)
        {
        }

        /// <summary>
        /// Saves the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="memento">The memento.</param>
        /// <param name="oldState">The old state.</param>
        /// <param name="newState">The new state.</param>
        protected override void Save(Guid id, object memento, string oldState, out string newState)
        {
            var data = default(Data);
            if (this.store.TryGetValue(id, out data))
            {
                if (data.State != oldState)
                {
                    // TODO (Cameron): This definitely shouldn't be here...
                    throw new ConcurrencyException("Invalid state");
                }
            }
            else if (oldState != null)
            {
                // TODO (Cameron): Not sure if this should be here...
                throw new ConcurrencyException("Aggregate root does not exist.");
            }

            data = data ?? new Data();

            data.Value = memento;
            data.State = newState = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

            this.store[id] = data;
        }

        /// <summary>
        /// Loads the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="state">The state.</param>
        /// <returns>The memento.</returns>
        protected override object Load(Guid id, out string state)
        {
            var data = default(Data);
            if (!this.store.TryGetValue(id, out data))
            {
                state = null;
                return null;
            }

            state = data.State;

            return data.Value;
        }

        private class Data
        {
            public object Value { get; set; }

            public string State { get; set; }
        }
    }
}
