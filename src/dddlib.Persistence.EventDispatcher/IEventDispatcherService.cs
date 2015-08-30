namespace dddlib.Persistence.EventDispatcher
{
    /// <summary>
    /// Exposes the public members of the event dispatcher service.
    /// </summary>
    public interface IEventDispatcherService
    {
        /// <summary>
        /// Starts the service.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the service.
        /// </summary>
        void Stop();
    }
}
