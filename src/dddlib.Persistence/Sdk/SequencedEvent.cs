namespace dddlib.Persistence.Sdk;

/// <summary>
/// An event together with its position in the store-wide sequence of committed events.
/// </summary>
public sealed record SequencedEvent(long SequenceNumber, object Event);
