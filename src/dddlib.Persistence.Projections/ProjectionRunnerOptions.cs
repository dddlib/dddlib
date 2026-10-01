namespace dddlib.Persistence.Projections;

/// <summary>
/// How a <see cref="ProjectionRunner"/> polls and pages.
/// </summary>
public sealed class ProjectionRunnerOptions
{
    /// <summary>
    /// Gets the most events read and applied in one transaction. Catch-up is the expensive case, so the default is
    /// larger than the event dispatcher's.
    /// </summary>
    public int BatchSize { get; init; } = 500;

    /// <summary>
    /// Gets the delay before polling again after an empty poll. It doubles on each consecutive empty poll up to
    /// <see cref="MaxPollingInterval"/> and resets when a page is found.
    /// </summary>
    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxPollingInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the delay after a failure before the page is read and applied again.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(30);
}
