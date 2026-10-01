namespace dddlib.Persistence.Sdk;

/// <summary>
/// A page of the <see cref="IEventFeed"/>: the events it holds and the sequence number it extends to. When the page
/// was read for particular event types, <see cref="Events"/> holds only those, but the page still covers every event
/// up to <see cref="EndSequenceNumber"/>, so a reader that records that number as its checkpoint skips the others.
/// </summary>
/// <param name="EndSequenceNumber">
/// The sequence number of the last event the page covers, or the number the page was read after when there were no
/// more events.
/// </param>
/// <param name="Events">The events of the requested types, in sequence order.</param>
public sealed record EventPage(long EndSequenceNumber, IReadOnlyList<FeedEvent> Events);
