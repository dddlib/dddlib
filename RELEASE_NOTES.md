# Release notes

## 2.0.0 (unreleased)

Ground-up port of dddlib to .NET 10. See `docs/migrating-from-v1.md` for the breaking changes.

- Core: `AggregateRoot`, `Entity`, `ValueObject<T>`, natural keys, bootstrappers, event application, mapping,
  mementos, all as in v1. Source generator and analyzers ship in the `dddlib` package.
- Persistence: async event store and memento repositories for in-memory and SQL Server, System.Text.Json
  serialization, manually run schema scripts.
- Test framework: helpers to inspect uncommitted events, mementos and revisions and to validate mementos.
