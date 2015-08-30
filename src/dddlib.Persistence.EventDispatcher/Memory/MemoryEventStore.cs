namespace dddlib.Persistence.EventDispatcher.Memory
{
    using dddlib.Persistence.EventDispatcher.Sdk;

    internal class MemoryEventStore : IEventStore
    {
        public Batch GetNextUndispatchedEventsBatch(int batchSize)
        {
            throw new System.NotImplementedException();
        }

        public void MarkEventAsDispatched(long eventId)
        {
            throw new System.NotImplementedException();
        }
    }
}
