namespace dddlib.Persistence.EventDispatcher;

/// <summary>
/// How an <see cref="Sdk.EventDispatcher"/> polls and batches.
/// </summary>
public sealed class EventDispatcherOptions
{
    /// <summary>
    /// Gets the identity of the dispatcher. Each dispatcher id keeps its own position in the event sequence, so
    /// several consumers can dispatch the same events independently.
    /// </summary>
    public Guid DispatcherId { get; init; } = Guid.Empty;

    public int BatchSize { get; init; } = 50;

    /// <summary>
    /// Gets the delay before polling again after an empty poll. It doubles on each consecutive empty poll up to
    /// <see cref="MaxPollingInterval"/> and resets when a batch is found.
    /// </summary>
    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxPollingInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets how long a batch may stay incomplete before its undispatched events are handed out again. Also the delay
    /// after a dispatch failure, so that the failed batch is retried in order rather than skipped.
    /// </summary>
    public TimeSpan BatchTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
