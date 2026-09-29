using System.Globalization;
using dddlib.Runtime;
using dddlib.Sdk;
using dddlib.Sdk.Configuration.Model;

namespace dddlib;

/// <summary>
/// Represents an aggregate root: the entity through which all changes to an aggregate are applied, as events.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private static readonly IMapperProvider DefaultMap = new DefaultMapperProvider();

    private readonly List<object> events = [];
    private readonly IEventDispatcher eventDispatcher;
    private readonly bool persistEvents;

    protected AggregateRoot()
        : this(static type => Application.Current.GetAggregateRootType(type))
    {
    }

    protected AggregateRoot(AggregateRootType aggregateRootType)
        : this(_ => aggregateRootType ?? throw new ArgumentNullException(nameof(aggregateRootType)))
    {
    }

    private AggregateRoot(Func<Type, AggregateRootType> resolveAggregateRootType)
        : base(resolveAggregateRootType)
    {
        var aggregateRootType = (AggregateRootType)this.Metadata;
        this.eventDispatcher = aggregateRootType.EventDispatcher;
        this.persistEvents = aggregateRootType.PersistEvents;
    }

    internal string? State { get; private set; }

    internal int Revision { get; private set; }

    /// <summary>
    /// Gets the mapper provider used to map between entities, value objects and events.
    /// </summary>
    protected IMapperProvider Map => DefaultMap;

    internal void Initialize(object? memento, int revision, IEnumerable<object> events, string? state)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentOutOfRangeException.ThrowIfNegative(revision);

        if (memento is not null)
        {
            this.SetState(memento);
            this.Revision = revision;
        }

        foreach (var @event in events)
        {
            this.Apply(@event, isNew: false);
        }

        this.State = state;
    }

    internal object? GetMemento() => this.GetState();

    internal IReadOnlyList<object> GetUncommittedEvents() => this.events;

    internal void CommitEvents(string state)
    {
        ArgumentException.ThrowIfNullOrEmpty(state);

        this.events.Clear();
        this.State = state;
    }

    /// <summary>
    /// Gets a memento representing the state of this aggregate root, or <see langword="null"/> if the aggregate
    /// root does not support mementos.
    /// </summary>
    protected virtual object? GetState() => null;

    /// <summary>
    /// Sets the state of this aggregate root from the specified memento.
    /// </summary>
    protected virtual void SetState(object memento)
    {
        throw new RuntimeException(
            string.Format(
                CultureInfo.InvariantCulture,
                @"The aggregate root of type '{0}' has not been configured to reconstitute from a memento representing its state.
To fix this issue:
- override the 'SetState' method of the aggregate root to update its state from the specified memento.",
                this.GetType()))
        {
            HelpLink = "https://github.com/dddlib/dddlib/blob/main/docs/aggregate-root-mementos.md",
        };
    }

    /// <summary>
    /// Applies the specified event to this aggregate root. The event is dispatched to the matching handler and
    /// recorded as uncommitted if the aggregate root persists events.
    /// </summary>
    protected void Apply<T>(T @event)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(@event);

        this.ThrowIfLifecycleEnded();
        this.Apply(@event, isNew: true);
    }

    private void Apply(object @event, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (!@event.GetType().IsClass)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Unable to apply the specified change of type '{0}' to the aggregate root of type '{1}'.",
                    @event.GetType(),
                    this.GetType()));
        }

        try
        {
            this.eventDispatcher.Dispatch(this, @event);
        }
        catch (RuntimeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The event dispatcher of type '{0}' threw an exception whilst attempting to dispatch an event of type '{1}'.\r\nSee inner exception for details.",
                    this.eventDispatcher.GetType(),
                    @event.GetType()),
                ex);
        }

        this.Revision++;

        if (this.persistEvents && isNew)
        {
            this.events.Add(@event);
        }
    }
}
