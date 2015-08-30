namespace dddlib.Persistence.EventDispatcher
{
    /// <summary>
    /// Exposes the public members of the event dispatcher.
    /// </summary>
    public interface IEventDispatcher
    {
        /// <summary>
        /// Dispatches the specified event.
        /// </summary>
        /// <param name="eventId">The event identifier.</param>
        /// <param name="event">The event.</param>
        void Dispatch(long eventId, object @event);
    }
}
