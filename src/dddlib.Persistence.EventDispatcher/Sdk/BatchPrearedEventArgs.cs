namespace dddlib.Persistence.EventDispatcher.Sdk
{
    using System;

    /// <summary>
    /// Represents the set of arguments passed to the batch prepared event handler.
    /// </summary>
    public class BatchPrearedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BatchPrearedEventArgs"/> class.
        /// </summary>
        /// <param name="batchId">The batch identifier.</param>
        public BatchPrearedEventArgs(long batchId)
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
