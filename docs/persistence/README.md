# dddlib.Persistence

The persistence companion for **dddlib**. Two persistence models are supported, each with an in-memory implementation
for tests and a SQL Server implementation for production:

- [Memento persistence](memento-persistence.md): the aggregate root is stored as its current state, described by a
  [memento](../aggregate-root-mementos.md). This is the conventional model.
- [Event sourcing persistence](event-sourcing-persistence.md): the aggregate root is stored as the stream of
  [events](../aggregate-root-event-application.md) it has applied, optionally with [snapshots](event-sourcing-persistence.md#snapshotting).

Both models share:

- an identity map from natural key to stream identity, backed by a natural key repository;
- the same [serialization](serialization.md) of natural keys, events and mementos;
- the same exceptions: `PersistenceException` for a model or configuration problem (with the underlying
  `RuntimeException` as its inner exception), `ConcurrencyException` when a save conflicts with the stored state, and
  `AggregateRootNotFoundException` when nothing exists for a natural key or its lifecycle has ended;
- asynchronous APIs throughout. Every operation takes an optional `CancellationToken`.

The SQL Server implementations are in **dddlib.Persistence.SqlServer** and need their [schema](sql-server.md) installed
before first use with `SqlServerSchema.EnsureAsync`. Nothing in the library changes the schema implicitly, and
constructors do no I/O.

Downstream of the event store, the [event dispatcher](event-dispatcher.md) delivers committed events to your code at
least once, and [projections](projections.md) keep read models up to date from them, each event taking effect exactly
once.

## Requirements on the model

To be persisted, an aggregate root must have a natural key and a [reconstitution factory](../aggregate-root-reconstitution.md).
For the memento model it must also implement `GetState` and `SetState`; for the event model it must apply at least one
event on creation, or there is nothing to save. The repositories report each of these as a `PersistenceException`
whose message says what to add.

## Testing a model

The in-memory repositories make persistence tests fast and need no infrastructure. They serialize through JSON just
like SQL Server, so a loaded aggregate root never shares instances with the one that was saved, and serialization
problems surface without a database.
