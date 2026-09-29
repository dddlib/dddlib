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

Events applied to the aggregate root are not stored by this model; saving clears them. Storing them as well, so that
they can be dispatched, is tracked in [issue 149](https://github.com/dddlib/dddlib/issues/149).

## In-memory

```csharp
var repository = new dddlib.Persistence.Memory.MemoryRepository<Car>();

var car = new Car("W807ASB");
await repository.SaveAsync(car);

var sameCar = await repository.LoadAsync(car.Registration!);
```

Nothing is shared between instances of `MemoryRepository<T>`; pass a shared `MemoryIdentityMap` to the constructor to
share identities between repositories.

## SQL Server

`SqlServerMementoRepository<T>` stores any memento as JSON in the `Mementos` table created by the shipped scripts:

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
aggregate root; the stored state must match it or the save is a `ConcurrencyException`.

```csharp
public sealed class CarRepository(string connectionString)
    : dddlib.Persistence.SqlServer.SqlServerRepository<Car>(connectionString)
{
    protected override async Task<string> SaveAsync(Guid id, object memento, string? preCommitState, CancellationToken cancellationToken)
    {
        // MERGE into your table where State = @preCommitState (or insert when it is null),
        // and return the new state token
    }

    protected override async Task<dddlib.Persistence.Sdk.MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        // SELECT your columns; return null when there is no row,
        // otherwise new MementoResult(memento, state)
    }
}
```

## Concurrency

Each save presents the state token the aggregate root was loaded with. Saving an instance that was loaded before
another save, or after the aggregate root was destroyed and saved, throws `ConcurrencyException`. Saving a new
aggregate root whose natural key already exists throws `ConcurrencyException` with the message
"Aggregate root already exists."
