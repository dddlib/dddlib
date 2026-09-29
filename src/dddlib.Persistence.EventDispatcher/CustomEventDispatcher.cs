namespace dddlib.Persistence.EventDispatcher;

/// <summary>
/// An <see cref="IEventDispatcher"/> over a delegate.
/// </summary>
public sealed class CustomEventDispatcher : IEventDispatcher
{
    private readonly Func<long, object, CancellationToken, Task> dispatch;

    public CustomEventDispatcher(Func<long, object, CancellationToken, Task> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        this.dispatch = dispatch;
    }

    public CustomEventDispatcher(Action<long, object> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        this.dispatch = (sequenceNumber, @event, _) =>
        {
            dispatch(sequenceNumber, @event);
            return Task.CompletedTask;
        };
    }

    public Task DispatchAsync(long sequenceNumber, object @event, CancellationToken cancellationToken) =>
        this.dispatch(sequenceNumber, @event, cancellationToken);
}
