namespace dddlib.Persistence.EventDispatcher.Sdk;

/// <summary>
/// The exception an <see cref="IEventBatchStore"/> threw while the host polled for a batch or marked one dispatched.
/// </summary>
public sealed class StoreFailedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
