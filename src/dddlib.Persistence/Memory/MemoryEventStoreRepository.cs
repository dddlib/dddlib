// <copyright file="MemoryEventStoreRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using Sdk;

    /// <summary>
    /// Represents a memory-based event store repository.
    /// </summary>
    /// <seealso cref="dddlib.Persistence.Sdk.EventStoreRepository" />
    public sealed class MemoryEventStoreRepository : EventStoreRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryEventStoreRepository"/> class.
        /// </summary>
        public MemoryEventStoreRepository()
            : base(new MemoryIdentityMap(), new MemoryEventStore())
        {
        }
    }
}
