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

`SqlServerMementoRepository<T>` stores any memento as JSON in the `Mementos` table of the [dddlib schema](sql-server.md) and
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
the identity map on SQL Server, unless you [pass your own](#custom-identity-map)) and implement the two storage
methods. Write the memento in the transaction dddlib gives you:

```csharp
public sealed class CarRepository(string connectionString)
    : dddlib.Persistence.SqlServer.SqlServerRepository<Car>(connectionString)
{
    protected override async Task<string> SaveAsync(SqlTransaction transaction, Guid id, object memento, string? preCommitState, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SaveCar", transaction.Connection, transaction)
        {
            CommandType = CommandType.StoredProcedure,
        };

        // Your parameters, and the state: insert when @PreCommitState is null; otherwise update where
        // State = @PreCommitState and raise error 50409 when no row matches.
        command.Parameters.Add("@PreCommitState", SqlDbType.VarChar, 36).Value = (object?)preCommitState ?? DBNull.Value;
        var postCommitState = command.Parameters.Add("@PostCommitState", SqlDbType.VarChar, 36);
        postCommitState.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync(cancellationToken);

        return (string)postCommitState.Value;
    }

    protected override async Task<dddlib.Persistence.Sdk.MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        // SELECT your columns; return null when there is no row,
        // otherwise new MementoResult(memento, state)
    }
}
```

The pre-commit state is null for a new aggregate root; when the stored state does not match it, raise SQL Server error
50409 or throw a `ConcurrencyException`, and the save is a `ConcurrencyException`. Write only the memento: dddlib opens
the connection and the transaction, takes the aggregate root's lock before calling you, appends the uncommitted events
after you return, and commits. Do not commit, roll back or append events yourself; calling `AppendEventsAsync` here is
an `InvalidOperationException`. A repository written this way can also [save in a batch](#saving-several-aggregate-roots).

To give the [event dispatcher](event-dispatcher.md#receiving-events) context that the events do not carry, such as the
tenant of the request that saved them, override `EventMetadata`:

```csharp
protected override IReadOnlyDictionary<string, string>? EventMetadata =>
    new Dictionary<string, string> { ["tenant"] = tenant };
```

It is stored with each event beside dddlib's own `hostname` and `timestamp` entries, and the dispatcher receives all of
them. A key of `hostname` or `timestamp` is an `ArgumentException`, thrown before anything is written.

### Owning the transaction

The original way to write custom storage is still supported: override
`SaveAsync(Guid id, object memento, IReadOnlyList<object> events, string? preCommitState, CancellationToken)` instead,
open the connection and transaction yourself, and call `AppendEventsAsync` with that transaction and the new state token
before committing, so that a dispatcher never sees events for a memento that was not saved. Metadata is then passed to
`AppendEventsAsync` before the events. A repository written this way cannot take part in a batch, because it owns the
transaction it saves in: adding to a batch through it is a `NotSupportedException`. A repository that overrides
neither method fails its first save with a `NotSupportedException`.

### Moving a repository to the transactional hook

1. Change the override's signature to `SaveAsync(SqlTransaction transaction, Guid id, object memento, string? preCommitState, CancellationToken cancellationToken)`
   and create commands on `transaction.Connection` with `transaction`.
2. Delete the connection, `BeginTransactionAsync`, the catch that turns error 50409 into a `ConcurrencyException`, the
   call to `AppendEventsAsync` and the commit. Keep the procedure call and return the new state token.
3. Move any metadata passed to `AppendEventsAsync` into `EventMetadata`.

The stored procedures do not change, provided they run in the caller's transaction rather than committing their own.

### Custom identity map

By default the repository maps natural keys to stream identities with `SqlServerIdentityMap`, which writes to the
`NaturalKeys` table of the dddlib schema. When the identity already lives somewhere else, for example in a column of
the aggregate root's own table, pass a `dddlib.Persistence.Sdk.IIdentityMap` to the base constructor instead:

```csharp
public sealed class CarRepository(string connectionString)
    : dddlib.Persistence.SqlServer.SqlServerRepository<Car>(connectionString, new CarIdentityMap(connectionString))
```

`TryGetAsync` returns the identity stored for a natural key, or null. `GetOrAddAsync` is called when a new aggregate
root is saved and returns the identity its memento is then saved under. `RemoveAsync` is called when a destroyed
aggregate root is saved or loaded. Rows that existed before dddlib was adopted can then be loaded and saved without
anything being written to `NaturalKeys`. The events still go to the dddlib schema, named by the optional last
argument.

## Saving several aggregate roots

`IRepository<T>` saves one aggregate root of one type. To save aggregate roots of different types in one transaction,
each through its own repository, add them to a batch and save the batch: every memento is stored and every event
appended, or nothing is and no aggregate root changes, so the caller can reload and retry.

```csharp
var batch = new dddlib.Persistence.SqlServer.SqlServerBatch(connectionString);
cars.Add(batch, car);       // a SqlServerRepository<Car> with custom storage
owners.Add(batch, owner);   // a SqlServerMementoRepository<Owner>
await batch.SaveAsync();
```

`Add` is on `dddlib.Persistence.Sdk.Repository<T>`, which every repository here derives from. `dddlib.Persistence.IBatch`
has only `SaveAsync`, so code that should not know the storage, such as an application service, can take an `IBatch`
and hand it to repository methods that add to it; the composition root creates the `SqlServerBatch`, or a `MemoryBatch`
in tests.

- The mementos and events are taken when the batch saves, not when the aggregate roots are added.
- A batch is used once: adding to it or saving it again after `SaveAsync` has started, whether or not that save
  succeeded, is an `InvalidOperationException`.
- The same instance twice is an `ArgumentException`. Every aggregate root is validated before anything is written.
- A conflict is a `ConcurrencyException` that names the aggregate root's type and natural key. Two new aggregate roots
  with one natural key are a `ConcurrencyException` too, and nothing is written.
- One correlation identifier covers every event, and the events take contiguous sequence numbers in the order the
  aggregate roots were added.
- As for a single save, a new aggregate root's identity is registered before the write; when the write fails it stays
  registered and the next save of that natural key reuses it.

| Batch | Repositories that can take part |
|---|---|
| `dddlib.Persistence.Memory.MemoryBatch` | `MemoryRepository<T>` |
| `dddlib.Persistence.SqlServer.SqlServerBatch` | `SqlServerMementoRepository<T>`, and `SqlServerRepository<T>` written with [the transactional hook](#custom-storage) |

A repository refuses a batch of another kind with an `ArgumentException`, and a repository that cannot take part with a
`NotSupportedException`. A `SqlServerBatch` writes everything on its own connection, so its repositories must use the
same server, database and dddlib schema; another one is an `ArgumentException` when the aggregate root is added. The
SQL Server batch takes every aggregate root's lock first, in identity order, then writes the mementos and appends all the
events last; single saves take their locks in the same order, so batches and single saves cannot deadlock each other.

## Concurrency

Each save presents the state token the aggregate root was loaded with. Saving an instance that was loaded before
another save, or after the aggregate root was destroyed and saved, throws `ConcurrencyException`. Saving a new
aggregate root whose natural key already exists throws `ConcurrencyException` with the message
"Aggregate root already exists."
