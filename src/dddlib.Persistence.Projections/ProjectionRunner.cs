using System.Globalization;
using dddlib.Persistence.Projections.Sdk;
using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Projections;

/// <summary>
/// Keeps a projection up to date: reads the events after its checkpoint from an <see cref="IEventFeed"/>, a page at
/// a time, and has the <see cref="IProjectionStore"/> apply each page with the checkpoint that follows it. Run it with
/// <see cref="RunAsync"/> from a hosted service, or <see cref="Start"/> it to run on a background task until it is
/// stopped or disposed. Several runners may share a store: they never both apply a page, and a lost race just means
/// re-reading the checkpoint.
/// </summary>
public class ProjectionRunner : IAsyncDisposable
{
    private readonly IEventFeed feed;
    private readonly IProjectionStore store;
    private readonly ProjectionRunnerOptions options;
    private readonly TimeProvider timeProvider;
    private long checkpoint;
    private CancellationTokenSource? stopping;
    private Task? running;

    public ProjectionRunner(IEventFeed feed, IProjectionStore store, ProjectionRunnerOptions? options = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(store);

        options ??= new ProjectionRunnerOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BatchSize);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.PollingInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxPollingInterval, options.PollingInterval);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.RetryDelay, TimeSpan.Zero);

        if (store.EventTypes is { Count: 0 })
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "The projection '{0}' handles no events. Register handlers with When<TEvent> in its constructor.", store.Name),
                nameof(store));
        }

        this.feed = feed;
        this.store = store;
        this.options = options;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Raised when an iteration fails: the page could not be read or applied. The runner waits
    /// <see cref="ProjectionRunnerOptions.RetryDelay"/>, re-reads the checkpoint and tries again, so the page is
    /// retried in order. A lost race with another runner is not a failure and is not raised.
    /// </summary>
    public event EventHandler<ProjectionFailedEventArgs>? ProjectionFailed;

    /// <summary>
    /// Gets the name of the projection.
    /// </summary>
    public string Name => this.store.Name;

    /// <summary>
    /// Gets the checkpoint as this runner last read or committed it. Another runner may have moved it since; see
    /// <see cref="GetStatusAsync"/> for the stored value.
    /// </summary>
    public long Checkpoint => Volatile.Read(ref this.checkpoint);

    /// <summary>
    /// Reads the projection's checkpoint and the last sequence number in the store, for lag.
    /// </summary>
    public async Task<ProjectionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var stored = await this.store.GetCheckpointAsync(cancellationToken).ConfigureAwait(false);
        var last = await this.feed.GetLastSequenceNumberAsync(cancellationToken).ConfigureAwait(false);

        return new ProjectionStatus(this.Name, stored, last);
    }

    /// <summary>
    /// Runs the projection until the token is cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var delay = this.options.PollingInterval;
        var known = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!known)
                    {
                        this.SetCheckpoint(await this.store.GetCheckpointAsync(cancellationToken).ConfigureAwait(false));
                        known = true;
                    }

                    var page = await this.feed.ReadEventsAsync(this.checkpoint, this.options.BatchSize, this.store.EventTypes, cancellationToken).ConfigureAwait(false);
                    if (page.EndSequenceNumber == this.checkpoint)
                    {
                        // Idle: re-read the checkpoint before the next poll, so a purge (or another runner) is noticed
                        // without an event having to arrive.
                        known = false;
                        await Task.Delay(delay, this.timeProvider, cancellationToken).ConfigureAwait(false);
                        delay = Min(delay + delay, this.options.MaxPollingInterval);
                        continue;
                    }

                    delay = this.options.PollingInterval;

                    try
                    {
                        await this.store.ApplyAsync(page, this.checkpoint, cancellationToken).ConfigureAwait(false);
                        this.SetCheckpoint(page.EndSequenceNumber);
                    }
                    catch (ConcurrencyException)
                    {
                        // Another runner applied the page first: carry on from the checkpoint it left.
                        known = false;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    this.ProjectionFailed?.Invoke(this, new ProjectionFailedEventArgs(this.Name, ex));
                    known = false;
                    await Task.Delay(this.options.RetryDelay, this.timeProvider, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // stopping
        }
    }

    /// <summary>
    /// Starts the projection on a background task.
    /// </summary>
    public void Start()
    {
        if (this.running is not null)
        {
            throw new InvalidOperationException("The projection runner is already running.");
        }

        this.stopping = new CancellationTokenSource();
        this.running = Task.Run(() => this.RunAsync(this.stopping.Token));
    }

    /// <summary>
    /// Stops a runner started with <see cref="Start"/> and waits for it to finish the page in progress.
    /// </summary>
    public async Task StopAsync()
    {
        if (this.running is null || this.stopping is null)
        {
            return;
        }

        await this.stopping.CancelAsync().ConfigureAwait(false);
        await this.running.ConfigureAwait(false);

        this.stopping.Dispose();
        this.stopping = null;
        this.running = null;
    }

    public async ValueTask DisposeAsync()
    {
        await this.StopAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;

    private void SetCheckpoint(long value) => Volatile.Write(ref this.checkpoint, value);
}
