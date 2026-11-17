namespace dddlib.Persistence.Sdk;

/// <summary>
/// One stream's part of a commit to several streams: the events to append and the state token the stream must still
/// have, or null for a stream that must not exist yet.
/// </summary>
public sealed record StreamCommit(Guid StreamId, IReadOnlyList<object> Events, string? PreCommitState);
