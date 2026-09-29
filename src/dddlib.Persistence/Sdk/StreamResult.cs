namespace dddlib.Persistence.Sdk;

/// <summary>
/// The events read from a stream and the state token of its last commit (null for an unknown stream).
/// </summary>
public sealed record StreamResult(IReadOnlyList<object> Events, string? State);
