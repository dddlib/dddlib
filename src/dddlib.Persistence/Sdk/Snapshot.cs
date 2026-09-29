namespace dddlib.Persistence.Sdk;

/// <summary>
/// A memento of an aggregate root taken at a stream revision.
/// </summary>
public sealed record Snapshot(int StreamRevision, object? Memento);
