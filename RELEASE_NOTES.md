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
- Identity map: concurrent writers of different natural keys of the same aggregate root type no longer fail. They
  could exhaust their retries, see a primary key violation on removal, or deadlock. A natural key whose equal values
  always serialize alike (strings, integers, GUIDs and sealed value objects of them with default equality) is now
  found or added by the repository in one call (`IUniqueNaturalKeyRepository`, `INaturalKeySerializer.IsCanonical`),
  kept unique on SQL Server by an index on a hash of the serialized key. Any other key, such as one with a
  case-insensitive comparer, is still compared by the identity map with its own equality, now with a short random
  backoff between attempts. Removals retry conflicts in SQL. Schema version 3 (`dddlib03.sql`, expand only): every
  SQL Server package now requires version 3.
- Event metadata: a `SqlServerRepository<T>` can append metadata that the events do not carry, such as a tenant,
  through an `AppendEventsAsync` overload. It is stored beside dddlib's `hostname` and `timestamp`, and the event
  dispatcher receives all of it: `IEventDispatcher.DispatchAsync` now takes the metadata before the event (breaking),
  batches hold `DispatchedEvent`s, and `CustomEventDispatcher` takes a delegate with the metadata too. Schema version 4
  (`dddlib04.sql`) adds the metadata to `GetNextBatch`'s result, after the columns older dispatchers read: every SQL
  Server package now requires version 4 (issue 52).
- SQL Server: concurrent first saves no longer deadlock when they store types the database has not seen yet, such as
  new event types on a freshly created database. Registering a type in the `Types` table took range locks over the
  whole table, so two saves of different new types could deadlock, and the loser failed with a `SqlException`. Event
  stores, repositories, snapshots and the identity map all registered types this way. Concurrent adds and removals of
  natural keys of one aggregate root type also take turns now: they collided on the type's checkpoints and retried
  with no order, so under load the unluckiest waited long enough to time out. Schema version 5 (`dddlib05.sql`)
  changes procedure bodies only: every SQL Server package now requires version 5 (issue 55).
- Saving several aggregate roots in one transaction: every aggregate root is saved, or none is and none changes.
  `IEventStoreRepository` gains `SaveAsync` overloads that take a collection of aggregate roots of any types, with one
  correlation id for every event; the single-root overloads are unchanged. Event stores opt in through
  `Sdk.IMultiStreamEventStore`, which both shipped stores implement. Memento repositories add aggregate roots of any
  types to an `IBatch` with `Repository<T>.Add` (`IRepository<T>` is unchanged) and the batch saves them:
  `MemoryBatch` for `MemoryRepository<T>`, `SqlServerBatch` for `SqlServerMementoRepository<T>` and custom storage. A
  conflict is a `ConcurrencyException` naming the aggregate root's type and natural key. `SqlServerRepository<T>`
  gains a transactional `SaveAsync(SqlTransaction, ...)` that writes only the memento, with `EventMetadata` for
  metadata such as a tenant: dddlib owns the transaction, takes the aggregate root's lock first, appends the events and
  commits, and only a repository written this way can join a batch. Repositories that override the original
  `SaveAsync` keep working unchanged. Schema version 6 (`dddlib06.sql`, expand only) adds `CommitStreams`,
  `LockStreams`, `SaveMementos`, `AppendStreams` and their table types: every SQL Server package now requires
  version 6.
- Per-event metadata: each event can carry metadata of its own, such as a message ID, merged over the metadata of its
  save. `SqlServerRepository<T>` gains `GetEventMetadata(Guid id, object @event, int index)`, called once per event for
  saves through the transactional hook, alone or in a `SqlServerBatch`, and an `AppendEventsAsync` overload that takes
  a list in step with the events. Saves without it store exactly what they did before, and every event of a save now
  has the same `timestamp`. Schema version 7 (`dddlib07.sql`, expand only) adds an optional `@EventMetadata`
  parameter to `AppendEvents` and `AppendStreams`: every SQL Server package now requires version 7 (issue 61).
- Projections read event metadata: `FeedEvent` gains `Metadata`, before `Event` (a breaking change for code that
  constructs one, such as a test of a projection), with what the event dispatcher receives, so a handler that takes the
  envelope can project who saved an event and in which tenant. The in-memory store now keeps metadata too:
  `MemoryEventStore` stores `hostname` and `timestamp` with each event, `MemoryRepository<T>` gains `EventMetadata`
  and `GetEventMetadata` as `SqlServerRepository<T>` has them, and the in-memory dispatcher passes the metadata on
  instead of an empty dictionary. Schema version 8 (`dddlib08.sql`, expand only) returns the metadata from
  `ReadEvents` after the columns earlier versions read: every SQL Server package now requires version 8 (issue 60).
- The caller supplies the correlation id of a memento save, as it already could for the event store repository, so the
  events of one operation, such as a request or a message being handled, carry its id in the feed (`FeedEvent.CorrelationId`).
  `IRepository<T>` and `Repository<T>` gain `SaveAsync(T aggregateRoot, Guid correlationId, ...)`, and `IBatch`,
  `MemoryBatch` and `SqlServerBatch` gain `SaveAsync(Guid correlationId, ...)` for every event of the batch. Without
  one each commit gets a new id, as before; `Guid.Empty` is an `ArgumentException`. Breaking for custom storage: the
  protected `Repository<T>.SaveAsync(Guid id, object memento, IReadOnlyList<object> events, ...)` takes the
  `Guid correlationId` before `preCommitState`, and each `SqlServerRepository<T>.AppendEventsAsync` overload takes it
  before `state`, so a repository that owns its transaction passes it on. No schema change (issue 63).
- The event dispatcher marks each batch dispatched once, after its last dispatched event, rather than after every
  event, halving the round trips per event on SQL Server. A failure still marks the events before it, and stopping
  marks the events already dispatched, so only the rest are delivered again; a crash partway through a batch now
  redelivers that batch's dispatched events too. An `IEventBatchDispatcher` receives each batch whole, for example to
  publish it in one round trip, and is marked whole or not at all. No schema change (issue 59).
- The event dispatcher no longer stops when its store throws, for example on a transient SQL error while polling for a
  batch or marking one dispatched: it raises the new `StoreFailed` event, waits `BatchTimeout` so the affected batch
  comes back in order, and carries on.
- Identity map: every write of a type's natural keys on SQL Server takes the type's turn, and gives up clearly. A key
  the identity map compares itself (one that does not serialize canonically, such as a case-insensitive key) was added
  without waiting its turn, so it collided with other writers on the type's checkpoints; it now waits too, and is only
  sent back to synchronize when a key has been added since its checkpoint, not when one has been removed. A writer that
  waits more than 10 seconds for its turn now fails with a `ConcurrencyException` instead of SqlClient's command
  timeout. Many writers adding such keys to one type at once can still run out of attempts, as before. Schema version
  9 (`dddlib09.sql`) changes procedure bodies only: every SQL Server package now requires version 9 (issue 57).
- Dependencies: the SQL Server packages depend on `System.Security.Cryptography.Pkcs` 9.0.21, above the 9.0.18 that
  `Microsoft.Data.SqlClient` 7.1 brings, for the .NET denial of service vulnerabilities CVE-2026-84008 and
  CVE-2026-70162.
