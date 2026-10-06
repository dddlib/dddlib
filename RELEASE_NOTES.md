# Release notes

## 2.0.0 (unreleased)

Ground-up port of dddlib to .NET 10. See `docs/migrating-from-v1.md` for the breaking changes.

- Core: `AggregateRoot`, `Entity`, `ValueObject<T>`, natural keys, bootstrappers, event application, mapping,
  mementos, all as in v1. Source generator and analyzers ship in the `dddlib` package. `Apply` no longer requires
  an event to have a public parameterless constructor, so a positional record can be an event, and `ToMapToEvent`
  takes mappings that create such an event or return a copy of it (issue 48).
- Analyzers: DDDLIB001 to DDDLIB024 report at compile time the model mistakes that v1 reported at runtime or not at
  all, including the ones that depend on the bootstrapper (no reconstitution factory, no natural key, a mapping
  that is not configured), with code fixes for six of them (issue 2). DDDLIB023 reports an event or memento that
  is saved but fails to load, which the parameterless constructor `Apply` used to require ruled out for events, and
  DDDLIB024 a mapping that cannot create the event it is asked for or only creates one (issue 48). Entities and
  aggregate roots are designed for inheritance: DDDLIB025 reports one that is sealed, DDDLIB027 a reconstitution
  constructor a derived aggregate root cannot chain to, both with code fixes, and DDDLIB026 suppresses CA1852's
  advice to seal them. See `docs/source-generator.md`.
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
- Projections: `dddlib.Persistence.Projections` keeps read models up to date from the committed events, in memory or
  (with `dddlib.Persistence.Projections.SqlServer`) on SQL Server, as key/value views in dddlib's schema or in your own
  tables. A `Projection<TIdentity, TEntity>` registers `When<TEvent>` handlers; a `ProjectionRunner` reads the pages of
  events after the projection's checkpoint, for the event types it handles only, and commits each page with the
  checkpoint that follows it, so each event takes effect exactly once. It retries a failed page in order, reports
  lag, and rebuilds from the first event after a purge. Both event stores expose the store-wide feed as `IEventFeed`,
  with the stream identity on each event. The projection objects and the feed are schema version 2 (`dddlib02.sql`,
  expand only): every SQL Server package now requires version 2, which `EnsureAsync` applies.
