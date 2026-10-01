namespace dddlib.Persistence.Sdk;

/// <summary>
/// Reads the store-wide sequence of committed events, for projections and other readers that follow the whole store
/// rather than one stream. Separate from <see cref="IEventStore"/> so that custom event stores need not provide it.
/// </summary>
public interface IEventFeed
{
    /// <summary>
    /// Reads the next page of committed events: up to <paramref name="maxCount"/> events after
    /// <paramref name="afterSequenceNumber"/>, in sequence order. With <paramref name="eventTypes"/>, only events of
    /// exactly those types are returned, but the page still spans the same events, so its
    /// <see cref="EventPage.EndSequenceNumber"/> moves past the events that were left out. With null or an empty
    /// collection, every event is returned and one whose type cannot be resolved fails with a
    /// <see cref="PersistenceException"/>.
    /// </summary>
    Task<EventPage> ReadEventsAsync(long afterSequenceNumber, int maxCount, IReadOnlyCollection<Type>? eventTypes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the sequence number of the last committed event, or zero when there is none, so that a reader can report
    /// how far behind it is.
    /// </summary>
    Task<long> GetLastSequenceNumberAsync(CancellationToken cancellationToken = default);
}
