// <copyright file="MemoryEventStoreRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using dddlib.Persistence.Sdk;

    /// <summary>
    /// Represents a memory-based event store repository.
    /// </summary>
    /// <seealso cref="dddlib.Persistence.Sdk.EventStoreRepository" />
    public sealed class MemoryEventStoreRepository : EventStoreRepository, IDisposable
    {
        private readonly MemoryIdentityMap identityMap;
        private readonly MemoryEventStore eventStore;

        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryEventStoreRepository"/> class.
        /// </summary>
        public MemoryEventStoreRepository()
            : this(new MemoryIdentityMap(), new MemoryEventStore(), new MemorySnapshotStore())
        {
        }

        private MemoryEventStoreRepository(MemoryIdentityMap identityMap, MemoryEventStore eventStore, ISnapshotStore snapshotStore)
            : base(identityMap, eventStore, snapshotStore)
        {
            this.identityMap = identityMap;
            this.eventStore = eventStore;
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

            this.identityMap.Dispose();
            this.eventStore.Dispose();

            this.isDisposed = true;
        }
    }
}
