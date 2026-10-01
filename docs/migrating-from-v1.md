# Migrating from v1

v2 is a ground-up port of dddlib to modern .NET. The model API is largely unchanged; persistence changed shape. There
is no compatibility with data written by v1.

## Platform

- `net10.0`. The source generator and analyzers target `netstandard2.0` and need the .NET 9.0.300 SDK or later
  (Visual Studio 17.14 or later).
- Strong naming is gone.
- Guard clauses use `ArgumentNullException.ThrowIfNull` and friends; parameter names in argument exceptions are the
  plain parameter names.

## Model

- `[NaturalKey]` works as before; the attribute class is now `NaturalKeyAttribute`.
- `ValueObject<T>` works as before, including `ToUseEqualityComparer`. Equal collections now also produce equal hash
  codes, which they did not in v1.
- The two-level configuration wrapper interfaces (`IEntityConfigurationWrapper<TConfiguration, T>` and siblings)
  collapsed to one level. Code that only calls the fluent methods is unaffected.
- Declare domain types `partial` to get [generated code](source-generator.md) instead of reflection. This is optional.
- Event handlers are dispatched by exact event type, as before. A `protected` handler in a base class now runs once,
  not twice.
- `Apply` takes any class. An event no longer needs a public parameterless constructor, so a positional record can be
  an event. For such events `ToMapToEvent` also takes a [mapping](bootstrapper.md#mapping) that creates the event or
  returns a copy of it, and `ToEvent<T>()` no longer requires the constructor at compile time.
- `dddlib.Runtime.Application` no longer keeps a global stack of applications; the current application flows with the
  asynchronous context, so tests can create one per test in parallel.

## Persistence

- All repository, store and identity map operations are asynchronous and take a `CancellationToken`. `Save` becomes
  `SaveAsync`, `Load` becomes `LoadAsync`.
- `out` parameters became result records: `IEventStore.GetStreamAsync` returns a `StreamResult`,
  `IIdentityMap.TryGetAsync` returns `Guid?`, and custom `SqlServerRepository<T>` implementations return a
  `MementoResult`.
- Serialization is System.Text.Json instead of `JavaScriptSerializer`. Stored JSON and type names are not compatible
  with v1. Type names are stored without assembly version.
- The SQL Server implementations moved to the **dddlib.Persistence.SqlServer** package; the namespaces are unchanged.
- Meld is gone. Constructors no longer create or upgrade the schema; call `SqlServerSchema.EnsureAsync` from a
  migration step or at startup, or run `SqlServerSchema.GetScript` through a migration tool. The schema is versioned
  in its own `Versions` table as before, and a schema behind the package fails the first call with a
  `PersistenceException`. Unlike Meld, a schema ahead of the package is accepted (and reported by `EnsureAsync`), for
  rolling upgrades, until a script records that it no longer supports the package. The single-event `CommitStream2`
  procedure is gone. JSON columns are `NVARCHAR(MAX)`.
- The in-memory implementations are in-process only; the memory-mapped files and global mutexes that shared them
  across processes are gone.
- The memento repository also appends the aggregate root's uncommitted events to its event stream, in the same
  transaction, so that they can be dispatched ([issue 1](https://github.com/dddlib/dddlib/issues/1)). Custom
  `SqlServerRepository<T>` implementations receive them in `SaveAsync` and may append them with `AppendEventsAsync`
  or ignore them.

## Event dispatcher

`dddlib.Persistence.EventDispatcher` is back with the same batch model but polls the event store instead of using
`SqlDependency`, which Azure SQL does not support. `IEventDispatcher.Dispatch` is now `DispatchAsync` with a
`CancellationToken`; the host takes `EventDispatcherOptions` and runs with `RunAsync(token)` or `Start`/`StopAsync`;
the SQL Server host is in **dddlib.Persistence.EventDispatcher.SqlServer** and its tables are part of the one dddlib
schema. See [Event dispatcher](persistence/event-dispatcher.md).

## Projections

`dddlib.Projections`, which v1 never finished, is back as **dddlib.Persistence.Projections** and
**dddlib.Persistence.Projections.SqlServer**. `IRepository<TIdentity, TEntity>` and `MemoryRepository<TIdentity, TEntity>`
keep their names under the new namespaces, made asynchronous and with `GetAllAsync`; `SqlServerRepository<TIdentity, TEntity>`
stores views as JSON in dddlib's schema. What v1 left to the caller is provided: `Projection<TIdentity, TEntity>` with
`When<TEvent>` handlers, a `ProjectionRunner` that reads the store-wide event feed (v1's `GetEventsFrom`, now
`IEventFeed` on both event stores) and commits each page with its checkpoint, so an event takes effect on the read
model exactly once, and `SqlServerProjection` for projections into your own tables. The projection objects are
version 2 of the dddlib schema. See [Projections](persistence/projections.md).

Everything in v1 has now been ported.

`dddlib.TestFramework` is back with the same three extension methods plus `ModelValidator`; see
[Testing a Domain Model](testing.md).
