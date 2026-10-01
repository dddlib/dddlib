using System.Globalization;
using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Projections.Sdk;

/// <summary>
/// A projection: a name and the handlers for the events it projects, registered in the constructor with
/// <see cref="When{TEvent}(Func{TEvent, TContext, CancellationToken, Task})"/> and its overloads. Dispatch is by exact
/// event type, as it is for the handlers of an aggregate root; <c>When&lt;object&gt;</c> registers a catch-all that
/// receives every event no exact handler takes, and with one the projection reads every event rather than only the
/// registered types.
/// </summary>
/// <typeparam name="TContext">What a handler writes to: the views, or the transaction on the user's own tables.</typeparam>
public abstract class ProjectionBase<TContext>
{
    private readonly Dictionary<Type, Func<FeedEvent, object, TContext, CancellationToken, Task>> handlers = [];
    private Func<FeedEvent, object, TContext, CancellationToken, Task>? catchAll;

    protected ProjectionBase(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        this.Name = name;
    }

    /// <summary>
    /// Gets the name of the projection, under which its checkpoint and views are kept. Rebuild side by side under a
    /// new name (<c>cars-v2</c>) and switch readers over.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the event types the projection handles, or null when it has a catch-all handler and so handles every
    /// event.
    /// </summary>
    public IReadOnlyCollection<Type>? EventTypes => this.catchAll is null ? this.handlers.Keys : null;

    /// <summary>
    /// Applies an event through its handler, or does nothing when there is none. A handler that throws surfaces as a
    /// <see cref="ProjectionException"/> naming the projection, the sequence number and the event.
    /// </summary>
    public async Task ApplyAsync(FeedEvent @event, TContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var handler = this.handlers.GetValueOrDefault(@event.Event.GetType()) ?? this.catchAll;
        if (handler is null)
        {
            return;
        }

        try
        {
            await handler(@event, @event.Event, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProjectionException(this.Name, @event.SequenceNumber, @event.Event, ex);
        }
    }

    /// <summary>
    /// Registers the handler for events of exactly <typeparamref name="TEvent"/>.
    /// </summary>
    protected void When<TEvent>(Func<TEvent, TContext, CancellationToken, Task> handler)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        this.Register<TEvent>((_, @event, context, cancellationToken) => handler(@event, context, cancellationToken));
    }

    /// <summary>
    /// Registers a synchronous handler for events of exactly <typeparamref name="TEvent"/>.
    /// </summary>
    protected void When<TEvent>(Action<TEvent, TContext> handler)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        this.Register<TEvent>((_, @event, context, _) =>
        {
            handler(@event, context);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Registers the handler for events of exactly <typeparamref name="TEvent"/>, with the <see cref="FeedEvent"/>
    /// envelope for the sequence number and the stream the event belongs to.
    /// </summary>
    protected void When<TEvent>(Func<FeedEvent, TEvent, TContext, CancellationToken, Task> handler)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        this.Register<TEvent>((envelope, @event, context, cancellationToken) => handler(envelope, @event, context, cancellationToken));
    }

    private void Register<TEvent>(Func<FeedEvent, TEvent, TContext, CancellationToken, Task> handler)
        where TEvent : class
    {
        Func<FeedEvent, object, TContext, CancellationToken, Task> untyped = (envelope, @event, context, cancellationToken) =>
            handler(envelope, (TEvent)@event, context, cancellationToken);

        if (typeof(TEvent) == typeof(object))
        {
            this.catchAll = this.catchAll is null
                ? untyped
                : throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture, "The projection '{0}' already has a catch-all handler.", this.Name));
            return;
        }

        if (!this.handlers.TryAdd(typeof(TEvent), untyped))
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, "The projection '{0}' already handles '{1}'.", this.Name, typeof(TEvent)));
        }
    }
}
