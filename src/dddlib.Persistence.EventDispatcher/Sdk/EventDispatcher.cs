namespace dddlib.Persistence.EventDispatcher.Sdk;

/// <summary>
/// Polls an <see cref="IEventBatchStore"/> and delivers each batch to an <see cref="IEventDispatcher"/> in sequence
/// order, marking each batch dispatched once, after its last dispatched event. An <see cref="IEventBatchDispatcher"/>
/// receives each batch whole. Run it with <see cref="RunAsync"/> from a hosted service, or
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
    /// Raised when the batch store throws while polling for a batch or marking one dispatched. The host waits
    /// <see cref="EventDispatcherOptions.BatchTimeout"/>, so that a batch handed out or dispatched but not marked comes
    /// back in order, and carries on.
    /// </summary>
    public event EventHandler<StoreFailedEventArgs>? StoreFailed;

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
                EventBatch? batch;
                try
                {
                    batch = await this.batchStore
                        .GetNextBatchAsync(this.options.DispatcherId, this.options.BatchSize, this.options.BatchTimeout, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (IsStoreFailure(ex, cancellationToken))
                {
                    // The store may have handed out the batch before failing: let it time out so it comes back in order.
                    this.StoreFailed?.Invoke(this, new StoreFailedEventArgs(ex));
                    await Task.Delay(this.options.BatchTimeout, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (batch is null)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay = Min(delay + delay, this.options.MaxPollingInterval);
                    continue;
                }

                delay = this.options.PollingInterval;

                if (!await this.DispatchAsync(batch, cancellationToken).ConfigureAwait(false))
                {
                    // Let the abandoned or unmarked batch time out so its remaining events come back in order.
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

    // Anything but the host being stopped.
    private static bool IsStoreFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested;

    // A batch dispatcher takes the batch whole, so it is marked whole or not at all.
    private async Task<bool> DispatchAsync(EventBatch batch, CancellationToken cancellationToken)
    {
        if (this.dispatcher is not IEventBatchDispatcher batchDispatcher)
        {
            return await this.DispatchEachAsync(batch, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await batchDispatcher.DispatchAsync(batch.Events, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var first = batch.Events[0];
            this.DispatchFailed?.Invoke(this, new DispatchFailedEventArgs(first.SequenceNumber, first.Event, ex));
            return false;
        }

        return await this.MarkDispatchedAsync(batch.Events[^1].SequenceNumber, cancellationToken).ConfigureAwait(false);
    }

    // Marks once, after the last event dispatched: at the end of the batch, before giving up on a failure, or on the
    // way out when stopping, so only the events not yet dispatched are delivered again.
    private async Task<bool> DispatchEachAsync(EventBatch batch, CancellationToken cancellationToken)
    {
        DispatchedEvent? last = null;
        var dispatched = true;

        foreach (var @event in batch.Events)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await this.dispatcher.DispatchAsync(@event.SequenceNumber, @event.Metadata, @event.Event, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                this.DispatchFailed?.Invoke(this, new DispatchFailedEventArgs(@event.SequenceNumber, @event.Event, ex));
                dispatched = false;
                break;
            }

            last = @event;
        }

        if (last is not null && !await this.MarkDispatchedAsync(last.SequenceNumber, cancellationToken).ConfigureAwait(false))
        {
            dispatched = false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return dispatched;
    }

    // Returns false when the store fails, after raising StoreFailed. Stopping must not lose the record of what was
    // already dispatched, so a cancelled token does not cancel the mark.
    private async Task<bool> MarkDispatchedAsync(long sequenceNumber, CancellationToken cancellationToken)
    {
        var token = cancellationToken.IsCancellationRequested ? CancellationToken.None : cancellationToken;

        try
        {
            await this.batchStore.MarkDispatchedAsync(this.options.DispatcherId, sequenceNumber, token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (IsStoreFailure(ex, token))
        {
            this.StoreFailed?.Invoke(this, new StoreFailedEventArgs(ex));
            return false;
        }
    }
}
