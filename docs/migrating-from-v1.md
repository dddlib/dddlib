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
- The schema is created by running the shipped scripts manually. Meld and the runtime schema upgrade are gone, as is
  the single-event `CommitStream2` procedure. JSON columns are `NVARCHAR(MAX)`.
- The in-memory implementations are in-process only; the memory-mapped files and global mutexes that shared them
  across processes are gone.
- The memento repository still does not store events; see [issue 1](https://github.com/dddlib/dddlib/issues/1).

## Event dispatcher

`dddlib.Persistence.EventDispatcher` is back with the same batch model but polls the event store instead of using
`SqlDependency`, which Azure SQL does not support. `IEventDispatcher.Dispatch` is now `DispatchAsync` with a
`CancellationToken`; the host takes `EventDispatcherOptions` and runs with `RunAsync(token)` or `Start`/`StopAsync`;
the dispatcher schema is one script, `06-SqlServerEventDispatcher.sql`. See [Event dispatcher](persistence/event-dispatcher.md).

## Not yet ported

- `dddlib.Projections`.

`dddlib.TestFramework` is back with the same three extension methods plus `ModelValidator`; see
[Testing a Domain Model](testing.md).
