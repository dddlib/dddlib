namespace dddlib.Persistence.EventDispatcher.Sdk;

/// <summary>
/// An event to dispatch, with its position in the store-wide sequence of committed events and the metadata it was
/// stored with: dddlib's own entries and any the caller appended with it. An event store that keeps no metadata gives
/// none.
/// </summary>
public sealed record DispatchedEvent(long SequenceNumber, IReadOnlyDictionary<string, string> Metadata, object Event);
