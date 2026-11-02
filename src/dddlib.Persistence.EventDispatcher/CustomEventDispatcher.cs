namespace dddlib.Persistence.EventDispatcher;

/// <summary>
/// An <see cref="IEventDispatcher"/> over a delegate.
/// </summary>
public sealed class CustomEventDispatcher : IEventDispatcher
{
    private readonly Func<long, IReadOnlyDictionary<string, string>, object, CancellationToken, Task> dispatch;

    public CustomEventDispatcher(Func<long, IReadOnlyDictionary<string, string>, object, CancellationToken, Task> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        this.dispatch = dispatch;
    }

    /// <summary>
    /// Creates a dispatcher over a delegate that does not take the event's metadata.
    /// </summary>
    public CustomEventDispatcher(Func<long, object, CancellationToken, Task> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        this.dispatch = (sequenceNumber, _, @event, cancellationToken) => dispatch(sequenceNumber, @event, cancellationToken);
    }

    /// <summary>
    /// Creates a dispatcher over a delegate that does not take the event's metadata.
    /// </summary>
    public CustomEventDispatcher(Action<long, object> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        this.dispatch = (sequenceNumber, _, @event, _) =>
        {
            dispatch(sequenceNumber, @event);
            return Task.CompletedTask;
        };
    }

    public Task DispatchAsync(long sequenceNumber, IReadOnlyDictionary<string, string> metadata, object @event, CancellationToken cancellationToken) =>
        this.dispatch(sequenceNumber, metadata, @event, cancellationToken);
}
