# Release notes

## 2.0.0 (unreleased)

Ground-up port of dddlib to .NET 10. See `docs/migrating-from-v1.md` for the breaking changes.

- Core: `AggregateRoot`, `Entity`, `ValueObject<T>`, natural keys, bootstrappers, event application, mapping,
  mementos, all as in v1. Source generator and analyzers ship in the `dddlib` package.
- Persistence: async event store and memento repositories for in-memory and, in `dddlib.Persistence.SqlServer`,
  SQL Server, with System.Text.Json serialization. dddlib provides and upgrades its own versioned SQL Server schema
  through `SqlServerSchema.EnsureAsync`, and fails the first call with a clear message when the schema is behind the
  package (issue 43). Processes can be upgraded one at a time: a package keeps working against a newer schema until
  a script records that it no longer supports it, and `EnsureAsync` and `GetVersionAsync` return the schema version
  with `IsAhead` and `IsCompatible`. The memento repositories append the aggregate root's events to its
  stream in the same transaction, so the event dispatcher delivers them too.
- Test framework: helpers to inspect uncommitted events, mementos and revisions and to validate mementos.
- Event dispatcher: `dddlib.Persistence.EventDispatcher` delivers committed events in sequence order with
  at-least-once delivery, polling the in-memory or (with `dddlib.Persistence.EventDispatcher.SqlServer`) SQL Server
  event store (no `SqlDependency`, so Azure SQL works).
