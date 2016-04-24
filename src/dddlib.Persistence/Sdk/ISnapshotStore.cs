// <copyright file="ISnapshotStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;

    /// <summary>
    /// Exposes the public members of the snapshot store.
    /// </summary>
    public interface ISnapshotStore
    {
        /// <summary>
        /// Adds or updates the snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="snapshot">The snapshot.</param>
        void PutSnapshot(Guid streamId, Snapshot snapshot);

        /// <summary>
        /// Gets the snapshot for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <returns>The snapshot.</returns>
        Snapshot GetSnapshot(Guid streamId);
    }
}
