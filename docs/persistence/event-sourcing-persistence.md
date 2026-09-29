# Event Sourcing Persistence

The aggregate root is saved as the stream of events it applied since it was last saved, and loaded by reconstituting
an uninitialized instance and re-applying the stream. The repository interface is
`dddlib.Persistence.IEventStoreRepository`:

```csharp
public interface IEventStoreRepository
{
    Task SaveAsync<T>(T aggregateRoot, CancellationToken cancellationToken = default) where T : AggregateRoot;

    Task SaveAsync<T>(T aggregateRoot, Guid correlationId, CancellationToken cancellationToken = default) where T : AggregateRoot;

    Task<T> LoadAsync<T>(object naturalKey, CancellationToken cancellationToken = default) where T : AggregateRoot;
}
```

The correlation identifier is stored with every event of the commit; a new one is generated when none is given.

The aggregate root must use [event application](../aggregate-root-event-application.md): the first save of an
aggregate root that has applied no events is a `PersistenceException`. It must also have a
[reconstitution factory](../aggregate-root-reconstitution.md).

## In-memory

```csharp
var repository = new dddlib.Persistence.Memory.MemoryEventStoreRepository();

var car = new Car("W807ASB");
await repository.SaveAsync(car);

var sameCar = await repository.LoadAsync<Car>(car.Registration!);
```

`MemoryEventStoreRepository` composes `MemoryIdentityMap`, `MemoryEventStore` and `MemorySnapshotStore`; compose
`dddlib.Persistence.Sdk.EventStoreRepository` yourself to share or replace any of them.

## SQL Server

```csharp
var repository = new dddlib.Persistence.SqlServer.SqlServerEventStoreRepository(connectionString);

var car = new Car("W807ASB");
await repository.SaveAsync(car);

var sameCar = await repository.LoadAsync<Car>(car.Registration!);
```

An optional second argument selects the schema (default `dbo`); see [SQL Server](sql-server.md). Events are written
in one round trip through a table-valued parameter and stored as JSON with the stable name of their type.

## Concurrency

Each commit presents the state token returned by the previous commit to the same stream. A mismatch is a
`ConcurrencyException`: another commit happened first, the aggregate root was destroyed and saved, or a new aggregate
root's natural key already exists ("Aggregate root already exists."). The SQL Server implementation also reports a
failure to acquire the stream's commit lock within one second as a `ConcurrencyException`.

## Lifecycle

Saving a destroyed aggregate root (one whose lifecycle ended) removes its natural key from the identity map. A new
aggregate root with the same natural key can then be created and saved, with a fresh stream. Loading a destroyed
aggregate root throws `AggregateRootNotFoundException`.

## Snapshotting

Loading replays every event in the stream. For long streams, store a snapshot: the aggregate root's
[memento](../aggregate-root-mementos.md) at a stream revision. Loading then starts from the memento and replays only
the events after it.

```csharp
var identityMap = new SqlServerIdentityMap(connectionString);
var eventStore = new SqlServerEventStore(connectionString);
var snapshotStore = new SqlServerSnapshotStore(connectionString);
var repository = new EventStoreRepository(identityMap, eventStore, snapshotStore);

// ... after saving `car` ...
var streamId = await identityMap.TryGetAsync(typeof(Car), typeof(string), car.Registration!);
await snapshotStore.PutSnapshotAsync(streamId!.Value, new Snapshot(revision, memento));
```

The revision and memento come from the aggregate root; in application code obtain them through a method on the
aggregate root that calls `GetState`, since the persistence surface of `AggregateRoot` is internal. When to take
snapshots is up to the application.
