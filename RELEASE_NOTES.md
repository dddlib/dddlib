# Release notes

## 2.0.0 (unreleased)

Ground-up port of dddlib to .NET 10. See `docs/migrating-from-v1.md` for the breaking changes.

- Core: `AggregateRoot`, `Entity`, `ValueObject<T>`, natural keys, bootstrappers, event application, mapping,
  mementos, all as in v1. Source generator and analyzers ship in the `dddlib` package. `Apply` no longer requires
  an event to have a public parameterless constructor, so a positional record can be an event, and `ToMapToEvent`
  takes mappings that create such an event or return a copy of it (issue 48).
- Analyzers: DDDLIB001 to DDDLIB023 report at compile time the model mistakes that v1 reported at runtime or not at
  all, including the ones that depend on the bootstrapper (no reconstitution factory, no natural key, a mapping
  that is not configured), with code fixes for six of them (issue 2). DDDLIB023 reports an event or memento that
  is saved but fails to load, which the parameterless constructor `Apply` used to require ruled out for events
  (issue 48). See `docs/source-generator.md`.
- Persistence: async event store and memento repositories for in-memory and, in `dddlib.Persistence.SqlServer`,
  SQL Server, with System.Text.Json serialization. dddlib provides and upgrades its own versioned SQL Server schema
  through `SqlServerSchema.EnsureAsync`, and fails the first call with a clear message when the schema is behind the
  package (issue 43). Processes can be upgraded one at a time: a package keeps working against a newer schema until
  a script records that it no longer supports it, and `EnsureAsync` and `GetVersionAsync` return the schema version
  with `IsAhead` and `IsCompatible`. The memento repositories append the aggregate root's events to its
  stream in the same transaction, so the event dispatcher delivers them too. `SqlServerRepository<T>` accepts an
  `IIdentityMap`, for identities kept in the consumer's own tables (issue 45).
- Test framework: helpers to inspect uncommitted events, mementos and revisions and to validate mementos.
- Event dispatcher: `dddlib.Persistence.EventDispatcher` delivers committed events in sequence order with
  at-least-once delivery, polling the in-memory or (with `dddlib.Persistence.EventDispatcher.SqlServer`) SQL Server
  event store (no `SqlDependency`, so Azure SQL works).
