// <copyright file="BatchPreparedEventArgs.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Sdk
{
    using System;

    /// <summary>
    /// Represents the set of arguments passed to the batch prepared event handler.
    /// </summary>
    public class BatchPreparedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BatchPreparedEventArgs"/> class.
        /// </summary>
        /// <param name="batchId">The batch identifier.</param>
        public BatchPreparedEventArgs(long batchId)
        {
            this.BatchId = batchId;
        }

        /// <summary>
        /// Gets the batch identifier.
        /// </summary>
        /// <value>The batch identifier.</value>
        public long BatchId { get; private set; }
    }
}
