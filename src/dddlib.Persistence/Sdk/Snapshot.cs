// <copyright file="Snapshot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    /// <summary>
    /// Represents a snapshot.
    /// </summary>
    public class Snapshot
    {
        /// <summary>
        /// Gets or sets the stream revision for the snapshot.
        /// </summary>
        /// <value>The stream revision.</value>
        public int StreamRevision { get; set; }

        /// <summary>
        /// Gets or sets the memento for the snapshot.
        /// </summary>
        /// <value>The memento.</value>
        public object Memento { get; set; }
    }
}
