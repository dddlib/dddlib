// <copyright file="MemorySnapshotStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using dddlib.Persistence.Sdk;

    /// <summary>
    /// Represents a memory-based snapshot store.
    /// </summary>
    /// <seealso cref="dddlib.Persistence.Sdk.ISnapshotStore" />
    public class MemorySnapshotStore : ISnapshotStore
    {
        private readonly Dictionary<Guid, List<Snapshot>> snapshots = new Dictionary<Guid, List<Snapshot>>();

        /// <summary>
        /// Adds or updates the snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="snapshot">The snapshot.</param>
        public void PutSnapshot(Guid streamId, Snapshot snapshot)
        {
            Guard.Against.Null(() => snapshot);

            var streamSnapshots = default(List<Snapshot>);
            if (!this.snapshots.TryGetValue(streamId, out streamSnapshots))
            {
                streamSnapshots = new List<Snapshot>();
                this.snapshots.Add(streamId, streamSnapshots);
            }

            if (streamSnapshots.Any(streamSnapshot => streamSnapshot.StreamRevision == snapshot.StreamRevision))
            {
                throw new PersistenceException("Snapshot already exists for this revision.");
            }

            streamSnapshots.Add(snapshot);
        }

        /// <summary>
        /// Gets the latest snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <returns>The snapshot.</returns>
        public Snapshot GetSnapshot(Guid streamId)
        {
            var streamSnapshots = default(List<Snapshot>);
            if (!this.snapshots.TryGetValue(streamId, out streamSnapshots))
            {
                // NOTE (Cameron): There are no saved snapshots for this stream.
                return null;
            }

            return streamSnapshots.Single(x => x.StreamRevision == streamSnapshots.Max(y => y.StreamRevision));
        }
    }
}
