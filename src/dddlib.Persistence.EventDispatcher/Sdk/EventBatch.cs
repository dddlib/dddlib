using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.EventDispatcher.Sdk;

/// <summary>
/// A numbered batch of consecutive undispatched events, in sequence order.
/// </summary>
public sealed record EventBatch(long Id, IReadOnlyList<SequencedEvent> Events);
