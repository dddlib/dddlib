namespace dddlib.Persistence.EventDispatcher.Sdk
{
    /// <summary>
    /// Represents a batch of events.
    /// </summary>
    public class Batch
    {
        /// <summary>
        /// Gets or sets the batch identifier.
        /// </summary>
        /// <value>The batch identifier.</value>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the events.
        /// </summary>
        /// <value>The events.</value>
        public Event[] Events { get; set; }
    }
}
