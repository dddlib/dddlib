namespace dddlib.Persistence.Sdk;

/// <summary>
/// A stored memento and the state token of the commit that wrote it.
/// </summary>
public sealed record MementoResult(object Memento, string State);
