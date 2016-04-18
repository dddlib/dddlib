// <copyright file="MemorySnapshotStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using System.Collections.Generic;
    using dddlib.Persistence.Sdk;

    /// <summary>
    /// Represents the memory-based snapshot store.
    /// </summary>
    /// <seealso cref="dddlib.Persistence.Sdk.ISnapshotStore" />
    public class MemorySnapshotStore : ISnapshotStore
    {
        private readonly Dictionary<Guid, Snapshot> snapshots = new Dictionary<Guid, Snapshot>();

        /// <summary>
        /// Adds or updates the snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="snapshot">The snapshot.</param>
        public void PutSnapshot(Guid streamId, Snapshot snapshot)
        {
            Guard.Against.Null(() => snapshot);

            this.snapshots[streamId] = snapshot;
        }

        /// <summary>
        /// Gets the latest snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <returns>The snapshot.</returns>
        public Snapshot GetSnapshot(Guid streamId)
        {
            var snapshot = default(Snapshot);
            return this.snapshots.TryGetValue(streamId, out snapshot)
                ? snapshot
                : null;
        }
    }
}
