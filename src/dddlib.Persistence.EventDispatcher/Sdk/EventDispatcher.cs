namespace dddlib.Persistence.EventDispatcher.Sdk;

/// <summary>
/// Polls an <see cref="IEventBatchStore"/> and delivers each batch to an <see cref="IEventDispatcher"/> in sequence
/// order, marking events dispatched as it goes. Run it with <see cref="RunAsync"/> from a hosted service, or
/// <see cref="Start"/> it to run on a background task until it is stopped or disposed.
/// </summary>
public class EventDispatcher : IAsyncDisposable
{
    private readonly IEventDispatcher dispatcher;
    private readonly IEventBatchStore batchStore;
    private readonly EventDispatcherOptions options;
    private CancellationTokenSource? stopping;
    private Task? running;

    public EventDispatcher(IEventDispatcher dispatcher, IEventBatchStore batchStore, EventDispatcherOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(batchStore);

        options ??= new EventDispatcherOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BatchSize);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.PollingInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxPollingInterval, options.PollingInterval);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.BatchTimeout, TimeSpan.Zero);

        this.dispatcher = dispatcher;
        this.batchStore = batchStore;
        this.options = options;
    }

    /// <summary>
    /// Raised when the dispatcher throws. The batch is abandoned and retried after <see cref="EventDispatcherOptions.BatchTimeout"/>.
    /// </summary>
    public event EventHandler<DispatchFailedEventArgs>? DispatchFailed;

    /// <summary>
    /// Dispatches until the token is cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var delay = this.options.PollingInterval;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var batch = await this.batchStore
                    .GetNextBatchAsync(this.options.DispatcherId, this.options.BatchSize, this.options.BatchTimeout, cancellationToken)
                    .ConfigureAwait(false);

                if (batch is null)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay = Min(delay + delay, this.options.MaxPollingInterval);
                    continue;
                }

                delay = this.options.PollingInterval;

                if (!await this.DispatchAsync(batch, cancellationToken).ConfigureAwait(false))
                {
                    // Let the abandoned batch time out so its remaining events come back in order.
                    await Task.Delay(this.options.BatchTimeout, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // stopping
        }
    }

    /// <summary>
    /// Starts dispatching on a background task.
    /// </summary>
    public void Start()
    {
        if (this.running is not null)
        {
            throw new InvalidOperationException("The event dispatcher is already running.");
        }

        this.stopping = new CancellationTokenSource();
        this.running = Task.Run(() => this.RunAsync(this.stopping.Token));
    }

    /// <summary>
    /// Stops a dispatcher started with <see cref="Start"/> and waits for it to finish the event in progress.
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

    private async Task<bool> DispatchAsync(EventBatch batch, CancellationToken cancellationToken)
    {
        foreach (var @event in batch.Events)
        {
            try
            {
                await this.dispatcher.DispatchAsync(@event.SequenceNumber, @event.Event, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                this.DispatchFailed?.Invoke(this, new DispatchFailedEventArgs(@event.SequenceNumber, @event.Event, ex));
                return false;
            }

            await this.batchStore.MarkDispatchedAsync(this.options.DispatcherId, @event.SequenceNumber, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
