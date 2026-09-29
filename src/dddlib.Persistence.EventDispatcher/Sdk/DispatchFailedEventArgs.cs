namespace dddlib.Persistence.EventDispatcher.Sdk;

public sealed class DispatchFailedEventArgs(long sequenceNumber, object @event, Exception exception) : EventArgs
{
    public long SequenceNumber { get; } = sequenceNumber;

    public object Event { get; } = @event;

    public Exception Exception { get; } = exception;
}
