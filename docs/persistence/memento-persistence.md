# Memento Persistence

The conventional persistence model: the aggregate root is saved as its current state, described by a
[memento](../aggregate-root-mementos.md), and loaded by reconstituting an uninitialized instance from that memento.
The repository interface is `dddlib.Persistence.IRepository<T>`:

```csharp
public interface IRepository<T> where T : AggregateRoot
{
    Task SaveAsync(T aggregateRoot, CancellationToken cancellationToken = default);

    Task<T> LoadAsync(object naturalKey, CancellationToken cancellationToken = default);
}
```

## Events

Events applied to the aggregate root are appended to its event stream when the memento is saved, in the same
transaction, so that the [event dispatcher](event-dispatcher.md) delivers them. They are not used to load the
aggregate root: the memento remains the source of state, and an aggregate root that changes state without applying
events is saved without appending anything.

Concurrency is checked against the memento's state token. Whenever events are appended, the stream takes that token
as its own and its revision advances by the number of events, so the stream of an aggregate root persisted this way
carries the token of the last save that appended events. Do not persist the same aggregate root through both this
model and the event store repository.

## In-memory

```csharp
var eventStore = new dddlib.Persistence.Memory.MemoryEventStore();
var repository = new dddlib.Persistence.Memory.MemoryRepository<Car>(new MemoryIdentityMap(), eventStore);

var car = new Car("W807ASB");
await repository.SaveAsync(car);

var sameCar = await repository.LoadAsync(car.Registration!);
```

The events go to the `MemoryEventStore` the repository was composed with; compose a `MemoryEventDispatcher` with the
same instance to have them dispatched. The other constructors create a private event store, so the events are kept
but not observable. Nothing is shared between instances of `MemoryRepository<T>`; pass a shared `MemoryIdentityMap`
to share identities between repositories.

## SQL Server

`SqlServerMementoRepository<T>` stores any memento as JSON in the `Mementos` table created by the shipped scripts and
appends the events to the event store's `Streams` and `Events` tables in the same stored procedure:

```csharp
var repository = new dddlib.Persistence.SqlServer.SqlServerMementoRepository<Car>(connectionString);

var car = new Car("W807ASB");
await repository.SaveAsync(car);

var sameCar = await repository.LoadAsync(car.Registration!);
```

An optional second argument selects the schema (default `dbo`); see [SQL Server](sql-server.md).

## Custom storage

To store the state in a table shaped for the aggregate root, derive from `SqlServerRepository<T>` (which supplies
the identity map on SQL Server) and implement the two storage methods. The pre-commit state is null for a new
aggregate root; the stored state must match it or the save is a `ConcurrencyException`. The events are the
uncommitted events of the aggregate root, possibly none. To have them dispatched, write the memento in a transaction
and call `AppendEventsAsync` with that transaction and the new state token before committing, so that a dispatcher
never sees events for a memento that was not saved. A repository that has no use for the events can ignore them.

```csharp
public sealed class CarRepository(string connectionString)
    : dddlib.Persistence.SqlServer.SqlServerRepository<Car>(connectionString)
{
    protected override async Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, string? preCommitState, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        // MERGE into your table where State = @preCommitState (or insert when it is null), in the transaction,
        // and keep the new state token
        var state = ...;

        await this.AppendEventsAsync(transaction, id, events, state, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return state;
    }

    protected override async Task<dddlib.Persistence.Sdk.MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        // SELECT your columns; return null when there is no row,
        // otherwise new MementoResult(memento, state)
    }
}
```

`AppendEventsAsync` calls the `AppendEvents` procedure from script 05, which takes the same locks as the event
store's commit so that sequence numbers reflect commit order.

## Concurrency

Each save presents the state token the aggregate root was loaded with. Saving an instance that was loaded before
another save, or after the aggregate root was destroyed and saved, throws `ConcurrencyException`. Saving a new
aggregate root whose natural key already exists throws `ConcurrencyException` with the message
"Aggregate root already exists."
