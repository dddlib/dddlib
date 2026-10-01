namespace dddlib.Persistence.Sdk;

/// <summary>
/// An event read from the <see cref="IEventFeed"/>: its position in the store-wide sequence and the stream it belongs
/// to, so that a reader can key its work by the aggregate root without the event having to carry the natural key.
/// </summary>
/// <param name="SequenceNumber">The position in the store-wide sequence of committed events.</param>
/// <param name="StreamId">The identity of the stream, which is the aggregate root's identity in the identity map.</param>
/// <param name="StreamRevision">The one-based position of the event within its stream.</param>
/// <param name="CorrelationId">The correlation identity of the commit that wrote the event.</param>
/// <param name="Event">The event.</param>
public sealed record FeedEvent(long SequenceNumber, Guid StreamId, int StreamRevision, Guid CorrelationId, object Event)
    : SequencedEvent(SequenceNumber, Event);
