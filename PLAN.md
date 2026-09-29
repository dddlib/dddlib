# dddlib v2 port plan

This repository is a ground-up port of [dddlib](https://github.com/dddlib/dddlib) to modern .NET.
The legacy source lives at `C:\Users\cameronfletcher\Development\code\git\dddlib\dddlib` (branch `dev`,
last commit March 2017, .NET Framework 4.5). It is the reference, not the starting point: nothing is
copied wholesale, but its feature tests define the behaviour this port must reproduce.

## 1. Scope

In scope:

- `dddlib` core: `AggregateRoot`, `Entity`, natural keys, bootstrapper configuration, event application,
  entity and value object mapping, natural key serialization, lifecycle management.
- `dddlib.Persistence`, SQL Server only: identity map, natural key repository, type cache, event store,
  snapshot store, the event store repository built on them, and the memento-based `IRepository<T>` path
  (`Repository<T>`, `MemoryRepository<T>`, `SqlServerRepository<T>` for custom storage and
  `SqlServerMementoRepository<T>`).
- The in-memory persistence implementations, but only as far as they are needed to run the persistence
  scenarios without a database. They are cheap and make the repository tests fast.

Out of scope until everything above is green: `dddlib.Projections` and `perftest`. The old `dddlib.TestFramework`
package returns in phase 5 (its extension methods are needed by users testing their own models), and
`dddlib.Persistence.EventDispatcher` is ported in phase 6. When the event dispatcher is ported, its SQL Server notification service must not use
`SqlDependency`, which Azure SQL does not support; it should poll the event store instead (a polling listener).

## 2. Decisions already made

| Topic | Decision |
|---|---|
| Approach | Port, not in-place upgrade. Mechanical port first, redesign second, both behind the ported scenarios. |
| Package identity | Published under the existing `dddlib` NuGet package id as version 2.0. |
| Compatibility | None. No existing databases must be readable by v2. Namespaces, JSON format and stored type names are free choices. |
| Value objects | `ValueObject<T>` base class, as in v1. Records were tried first and rejected because record equality includes private fields. See section 4. |
| Target framework | `net10.0` for libraries and tests. `netstandard2.0` only for the source generator and analyzer projects, which Roslyn requires. |
| Test framework | TUnit. No xunit, no Xbehave. |
| Assertions | TUnit's built-in `Assert.That(...)`. No FluentAssertions. |
| Test databases | Testcontainers for .NET with the `mcr.microsoft.com/mssql/server` image, one container per test session, one database per test class. Docker is installed on the dev machine. |
| SQL client | `Microsoft.Data.SqlClient`. Table-valued parameters via `Microsoft.Data.SqlClient.Server.SqlDataRecord`. |
| JSON | `System.Text.Json`. The legacy `JavaScriptSerializer` does not exist on modern .NET. |
| Schema setup | SQL scripts shipped with the package and run manually before first use. No Meld, no runtime migration and no version table for now. No ILMerge. |
| Guards | `ArgumentNullException.ThrowIfNull` and friends. Replaces Guardian's expression-based `Guard.Against`. |
| API shape | Persistence is async end to end. `out` parameters become return records. Nullable reference types on everywhere. |
| Strong naming | Dropped unless a consumer needs it. The old `.snk` stays in the legacy repo. |
| Project layout | `slnx` solution, `Directory.Build.props`, `Directory.Packages.props` with central package management, `global.json` pinned to the installed 10.0.x SDK. |
| Code style | `.editorconfig` with the built-in .NET analyzers at their recommended level. No StyleCop ruleset. File-scoped namespaces, `var`, expression-bodied members where they read well. |

## 3. Target repository layout

```
dddlibv2/
  PLAN.md
  CLAUDE.md
  global.json
  Directory.Build.props
  Directory.Packages.props
  dddlib.slnx
  src/
    dddlib/                          core library
    dddlib.Persistence/              Sdk abstractions, Memory implementations, SqlServer implementations, Scripts/
    dddlib.Generators/               source generator + analyzers (phase 4)
  tests/
    dddlib.Tests/                    core feature scenarios, bug regressions, unit tests
    dddlib.Persistence.Tests/        persistence scenarios, integration tests
    dddlib.Tests.Support/            shared domain model (Vehicle, Registration, Wheel), test bootstrapper helpers, SQL container fixture
```

Persistence stays one assembly with `Memory` and `SqlServer` namespaces, as before. Split it only if a
consumer needs the abstractions without the SqlClient dependency.

## 4. Architecture notes for the port

### Value objects

`ValueObject<T>` stays (decided 2026-09-29, reversing an earlier plan to use C# records). Records were tried
first, but record equality includes every instance field, private ones included, which is the wrong semantics
for a value object: a cached or derived private field breaks equality. The name also matches the vocabulary of
the DDD book.

`ValueObject<T>` provides:

- Structural equality over public readable properties through `DefaultValueObjectEqualityComparer<T>`, compiled
  once per type as an expression. Properties that are `IEnumerable` (other than `string`) compare element by
  element; everything else compares through `EqualityComparer<TProperty>.Default`, so nested value objects and
  types that override `Equals` behave. Hash codes follow the same rules, so sequence-equal collection members
  hash equally (an improvement on v1).
- `==` and `!=`, a runtime-type mismatch check, and a configurable comparer through
  `configure.ValueObject<T>().ToUseEqualityComparer(...)`. The default comparer is created lazily so the
  bootstrapper can replace it; configuring it after first use is an error (Bug0128).
- `ToUseValueObjectSerializer(...)` and `ToMapToEvent<TEvent>(...)` as before. The generic constraint is
  `where T : ValueObject<T>` everywhere; records and other plain types are not value objects as far as dddlib is
  concerned.

A value object with no public properties fails at construction with a runtime exception unless a comparer is
configured. Phase 4 turns that into an analyzer diagnostic.

### Core

- `Application` remains the ambient registry of runtime type metadata (`AggregateRootType`, `EntityType`,
  `ValueObjectType`) so the mechanical port stays close to the original. Replace the global
  `List<Application>` stack with an `AsyncLocal<Application?>` scope so tests that push a per-test
  `Application` are safe under TUnit's parallel execution. Phase 4 makes most of the registry
  unnecessary by generating the metadata statically.
- `DefaultEventDispatcher` ports as reflection plus a compiled delegate (`Delegate.CreateDelegate` on an
  open instance method, or a cached expression) rather than `DynamicMethod` IL emit. It is the fallback
  path once the generator exists.
- Natural key serialization and value object serialization move to `System.Text.Json`. Keep a
  `JsonSerializerOptions` singleton that writes `DateTime` as ISO 8601 round-trip.
- `DefaultBootstrapperProvider` keeps assembly scanning for `IBootstrapper` in the mechanical phase.
- Runtime error messages keep their "To fix this issue" shape and wiki help links. They become analyzer
  diagnostics later, but the runtime checks stay for non-generated types.

### Persistence

- Port the T-SQL scripts nearly verbatim. They rely on `sp_getapplock`, `MERGE`, `SEQUENCE`, `THROW`
  and table-valued parameters, all of which work on SQL Server 2019+ and the container image.
- Error numbers 50409 (commit state mismatch) and 50500 (lock timeout) still map to `ConcurrencyException`. So does
  1222 (lock request timeout): `GetStream` reads the stream row under `HOLDLOCK` in a short transaction with a lock
  timeout instead of the legacy session-owned applock in tempdb, which could leak through connection pooling. The
  legacy single-event `CommitStream2` shortcut is dropped; one table-valued-parameter procedure commits all events.
  JSON columns are `NVARCHAR(MAX)` so unicode payloads survive.
- Interfaces become async: `IEventStore.GetStreamAsync`, `CommitStreamAsync`, `ISnapshotStore`,
  `INaturalKeyRepository`, `ITypeCache`, `IIdentityMap`, `IRepository<T>`, `IEventStoreRepository`.
  Return small records instead of `out` parameters, for example `StreamResult(IReadOnlyList<object> Events, string? State)`.
- Stored type names: since nothing must stay compatible, store a stable name that does not include
  assembly version, for example `Namespace.TypeName, AssemblyName`, and resolve through a registry rather
  than `Type.GetType` on an assembly-qualified string.
- Nothing in the library touches the schema at runtime. The scripts are run manually before first use; the
  test fixture runs them against the container database. Constructors must not do I/O.
- `TransactionScopeOption.Suppress` wrapping is kept so callers' ambient transactions do not leak in.

### Source generators and analyzers (phase 4)

Tracked in dddlib/dddlib#150 (Roslyn analyzer for Visual Studio).

How the generated code plugs in (implemented 2026-09-29): the generator emits a private nested `__DddlibMetadata`
class into every aggregate root, entity and value object that is `partial` (containing types included). It carries
exact-type event dispatch over the handlers the type declares, the natural key accessor, the uninitialized factory
and a value object comparer over the public properties. The runtime finds it once per type by name
(`GeneratedMetadata`), so private handlers and private nested types need no module initializers. Event dispatch
is composed one hierarchy level at a time (`EventDispatchers`), generated where a level is partial and reflection
where it is not, so mixed hierarchies work. The assembly's bootstrapper is registered at module initialization
(`BootstrapperRegistry`); scanning is the fallback.

Diagnostics: DDDLIB001 more than one natural key (error), DDDLIB002 value-type handler parameter, DDDLIB003 public
handler, DDDLIB004 value object without public properties (warnings), DDDLIB005 more than one bootstrapper,
DDDLIB006 bootstrapper without a public default constructor (errors), DDDLIB007 make the type partial (info).

Benchmarks (`benchmarks/dddlib.Benchmarks`, BenchmarkDotNet, 2026-09-29): both paths allocate nothing on `Apply`
and on natural key equality. Steady-state cost is the same within noise (Apply medians 27 ns generated versus 35 ns
reflection; equality 13 ns both): compiled expression delegates are as fast as generated code once jitted. The
generated path wins at type construction (no reflection, no expression compilation) and for trimming and AOT.

Each generator replaces one runtime mechanism and must leave the reflection path working for types
that are not `partial`:

| Runtime mechanism today | Generated replacement |
|---|---|
| Event handler dispatch by reflection | `switch` over event types in a partial aggregate |
| Natural key discovery, uninitialized factory | Static type metadata registered via module initializer |
| Bootstrapper discovery by assembly scan | Generated registration of `IBootstrapper` implementations |
| Runtime "To fix this issue" exceptions | Analyzer diagnostics: duplicate natural key, missing reconstitution constructor, handler for a value-type event, value object without public properties, etc. |
| Reflection-based JSON | A user-authored `JsonSerializerContext` registered through `JsonSerialization.AddTypeInfoResolver`. A generator cannot emit a context for System.Text.Json's own generator to fill in, because generators do not see each other's output. |

## 5. Phases

Each phase ends with a green test run and a commit. Do not start a later phase with a red earlier one.

### Phase 0: scaffold

- `global.json`, `Directory.Build.props` (nullable, implicit usings, warnings as errors, latest C#),
  `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `dddlib.slnx`.
- Empty `src/dddlib`, `tests/dddlib.Tests`, `tests/dddlib.Tests.Support` projects. Use `dotnet new TUnit`
  for the test projects, then move them under central package management.
- One smoke test proving TUnit runs from `dotnet test`.

Exit: `dotnet build` and `dotnet test` succeed on an empty suite.

### Phase 1: core library, mechanical port

Port in this order, writing the scenarios for each piece before or alongside it:

1. Value object conventions: `ValueObject<T>`, `DefaultValueObjectEqualityComparer<T>`, `Registration` in the
   support project. Scenarios: ValueObjectEquality.
2. `Entity`, natural keys, `NaturalKeyAttribute`, `DefaultTypeAnalyzerService`, `Application`.
   Scenarios: EntityEquality (18), EntityLifecycleManagement (1).
3. `AggregateRoot`, event application, `DefaultEventDispatcher`, reconstitution factory.
   Scenarios: AggregateRootEquality (16), AggregateRootEventApplication (3), AggregateRootLifecycleManagement (2).
4. Mapping: `IMapperProvider`, entity and value object mappers, reverse mappings.
   Scenarios: AggregateRootEntityMapping (2), AggregateRootValueObjectMapping (6).
5. Natural key and value object serialization on STJ. Scenarios: ValueObjectSerialization (2).
6. Memento support and the model validator helper. Scenarios: ModelValidationFeature (2).
7. `BusinessException` (1), bug regressions Bug0001, 0017, 0092, 0128, 0129, and the unit tests
   (AggregateRootTests 11, ApplicationTests 5, DefaultTypeAnalyzerServiceTests 1, serializer tests 2).

Exit: every scenario in section 7 for the core library is green.

### Phase 2: persistence abstractions and in-memory implementations

- `IIdentityMap`, `DefaultIdentityMap`, `INaturalKeyRepository`, `INaturalKeySerializer`, `IEventStore`,
  `ISnapshotStore`, `Snapshot`, `ITypeCache`, `EventStoreRepository`, `IRepository<T>`, `Repository<T>`,
  exceptions. (`AggregateRootFactory` already lives in the core `dddlib.Sdk` namespace.)
- `MemoryEventStore`, `MemoryNaturalKeyRepository`, `MemorySnapshotStore`, `MemoryIdentityMap`,
  `MemoryEventStoreRepository`, `MemoryRepository<T>`.
- Scenarios: MemoryEventPersistence (9), MemoryMementoPersistence (1), MemoryEventStoreTests (6),
  JsonSerializerTests (4), bug regressions Bug0043, 0064, 0081, 0109 (their memory parts, covering both
  repositories). Bug0127 and the SQL Server parts of Bug0109 land in phase 3.

Exit: all memory persistence scenarios green with no database.

### Phase 3: SQL Server persistence

- Testcontainers fixture in `dddlib.Tests.Support`, shared per test session with
  `[ClassDataSource<SqlServerContainer>(Shared = SharedType.PerTestSession)]`. Each test class creates its
  own database from the container's connection string and drops it on dispose.
- SQL scripts under `src/dddlib.Persistence/Scripts`, packaged as content and run manually. Since there is no
  upgrade path from v1, one script per component (`Persistence`, `NaturalKey`, `EventStore`, `SnapshotStore`,
  `MementoRepository`). The test fixture runs them on each per-class database.
- `SqlServerTypeCache`, `SqlServerNaturalKeyRepository`, `SqlServerIdentityMap`, `SqlServerEventStore`,
  `SqlServerSnapshotStore`, `SqlServerEventStoreRepository`, `SqlServerRepository<T>`, `SqlServerMementoRepository<T>`.
- Scenarios: SqlServerEventPersistence (9), SqlServerMementoPersistence (2), SqlServerEventStoreTests (6),
  SqlServerIdentityMapTests (7), SqlServerNaturalKeyRepositoryTests (1), SqlServerSnapshotStoreTests (2),
  and the SQL Server parts of Bug0109 plus Bug0127. UpgradeDatabaseVersionTests is dropped while schema setup
  is manual.

Exit: all SQL Server scenarios green against the container. CI can run them because Docker is the
only prerequisite.

### Phase 4: generators, analyzers, and API polish

- Add `src/dddlib.Generators` and reference it from the test projects as an analyzer.
- Introduce one generator at a time in the order of the table in section 4, keeping every scenario green.
  Add a second copy of the affected scenarios that uses `partial` types so both paths are covered.
- Convert the runtime checks that are statically detectable into diagnostics with the same wording.
- Review the public API: seal what should be sealed, mark SDK types with `EditorBrowsable(Never)` as
  before, and produce a public API snapshot test.

Exit: benchmarks (optional BenchmarkDotNet project) show the generated paths allocate nothing on
`Apply` and on natural key lookup. Met; see section 4.

### Phase 5: packaging

- Versioning with [MinVer](https://github.com/adamralph/minver): the version comes from git tags (`v2.0.0`,
  prerelease `v2.0.0-alpha.1`), height since the tag gives the prerelease suffix on untagged builds. The
  `VersionPrefix` in `Directory.Build.props` goes; `RELEASE_NOTES.md` stays as human-written notes only.
- NuGet metadata, SourceLink, deterministic builds, `RELEASE_NOTES.md`, GitHub Actions running
  `dotnet test` with the SQL container. The `dddlib` package carries the analyzer assembly under
  `analyzers/dotnet/cs` so consumers get generation and diagnostics without a second package.
- `dddlib.TestFramework` package (`src/dddlib.TestFramework`): the `GetUncommittedEvents`, `GetMemento` and
  `GetRevision` extension methods and `ModelValidator`, moved out of the test support project and given
  `InternalsVisibleTo` access to the core. The test support project consumes it.
- Publishing: Cameron has lost nuget.org access (support ticket raised, 2026-09-29). Until it is restored, packages
  are pushed as MinVer prereleases to a private GitHub Packages feed from GitHub Actions, which needs the repository
  on GitHub (no remote exists yet) and a token with `write:packages`. nuget.org publishing is added when access
  returns; the package ids stay `dddlib`, `dddlib.Persistence`, `dddlib.TestFramework`.

### Phase 6: event dispatcher

Port `dddlib.Persistence.EventDispatcher` as its own package, after phase 5 so the other packages are usable
meanwhile.

- Keep the batch model: the dispatcher event store hands out numbered batches of undispatched events per
  dispatcher id and marks events dispatched; the `EventDispatcher` host processes one buffered batch at a time.
- Replace the SqlDependency-based `INotificationService` with polling: a configurable interval with backoff when no
  events are found, a `CancellationToken` throughout, and a shape that hosts as an `IHostedService`. Azure SQL does
  not support `SqlDependency`. The in-memory implementation may notify in-process.
- Memory and SQL Server implementations, the dispatcher scripts consolidated to one file run manually like the
  others, and the three legacy test files: MemoryEventDispatcher, SqlServerEventDispatcher, SqlServerEventStoreTests.
- Issue dddlib/dddlib#149 (events on the memento path) is the natural follow-on once the dispatcher exists.


## 6. Test conventions with TUnit

Keep the structure that made the old suite readable: one feature per file, one nested class per
scenario, with the scenario's own `Subject` types and its own bootstrapper nested inside it. Replace
Xbehave's string steps with plain code and Given/When/Then comments.

```csharp
// As someone who uses dddlib [with event sourcing]
// In order to persist events
// I need to be able to record changes in state
public abstract class AggregateRootEventApplication : Feature
{
    public sealed class EventsAreStoredOnAggregate : AggregateRootEventApplication
    {
        [Test]
        public async Task Scenario()
        {
            // Given a natural key
            var naturalKey = "key";

            // When an aggregate root is instantiated with that natural key
            var aggregateRoot = new Subject(naturalKey);

            // Then an event is raised with that natural key
            var events = aggregateRoot.GetUncommittedEvents();
            await Assert.That(events).HasSingleItem();
            await Assert.That(((NewSubject)events.Single()).NaturalKey).IsEqualTo(naturalKey);
        }

        public class Subject : AggregateRoot { /* as in the legacy test */ }
        private class NewSubject { public string? NaturalKey { get; set; } }
        private class Bootstrapper : IBootstrap<Subject> { /* configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject()); */ }
    }
}
```

Rules:

- `Feature` base class creates a fresh `Application` in a `[Before(HookType.Test)]` hook using a
  bootstrapper provider that finds the nested `IBootstrap<T>` classes of the scenario, exactly as the
  legacy `FeatureBootstrapperProvider` does, and disposes it in `[After(HookType.Test)]`.
- Because the ambient `Application` is `AsyncLocal`, scenarios can run in parallel. If that proves
  flaky, put `[NotInParallel("Application")]` on `Feature` and remove it once phase 4 removes the need.
- SQL Server scenarios inherit `SqlServerFeature`, which injects the shared container and creates the
  per-class database. Mark them `[NotInParallel("SqlServer")]` only if the container shows lock
  contention; per-class databases should make parallel runs safe.
- Bug regressions live in `Bug/Bug0001.cs` and so on, named after the original GitHub issue number, with
  a link to the issue in a comment.
- Every legacy scenario name in section 7 must exist in the new suite with the same name, so parity can
  be checked with a grep. New scenarios are welcome, deleted ones are not.

## 7. Scenario inventory to port

Core (`tests/dddlib.Tests/Feature`):

- AggregateRootEntityMapping: EntityMappingWithEventCreation, EntityMappingWithEventMutation
- AggregateRootEquality: CaseInsensitiveEqualityComparerDefinedInBootstrapper, CaseSensitiveUndefinedEqualityComparer,
  CompositeNaturalKeyEqualityComparer, ConflictingNaturalKeySelectors, InheritedNaturalKeySelector,
  InheritedNaturalKeySelectorOveriddenInBootstrapper, InheritedNaturalKeySelectorOveriddenInSubclass,
  NaturalKeySelectorDefinedISubclass, NaturalKeySelectorDefinedInBaseClass, NaturalKeySelectorDefinedInBootstrapper,
  NaturalKeySelectorDefinedInBothBaseClassAndSubclass, NaturalKeySelectorDefinedInMetadata, NonConflictingNaturalKeySelectors,
  UndefinedNaturalKeySelector, UndefinedNaturalKeySelectorWithInheritance
- AggregateRootEventApplication: EventsAreStoredOnAggregate, EventsAreStoredOnInheritedAggregate, InheritedEventsAreStoredOnInheritedAggregate
- AggregateRootLifecycleManagement: DefaultLifecycle, EventBasedLifecycle
- AggregateRootValueObjectMapping: EntityMappingPartiallyUndefined, EntityMappingUndefined, ValueObjectMappingPartiallyUndefined,
  ValueObjectMappingUndefined, ValueObjectMappingWithEventCreation, ValueObjectMappingWithEventMutation
- BusinessException
- EntityEquality: same sixteen names as AggregateRootEquality plus NestedNaturalKeySelector,
  NestedNaturalKeySelectorWithBothInstancesHavingNullReference, NestedNaturalKeySelectorWithSingleInstanceHavingNullReference
- EntityLifecycleManagement: EntityLifecycle
- ModelValidationFeature: InvalidMementoImplementation, ValidMementoImplementation
- ValueObjectEquality: UndefinedEqualityComparer, EqualityComparerDefinedInBootstrapper, CaseSensitiveUndefinedEqualityComparer,
  CaseInsensitiveStringEqualityComparerDefinedInBootstrapper, CollectionMemberComparesBySequence, PrivateFieldsDoNotParticipateInEquality
- ValueObjectSerialization: CustomValueObjectSerializer, CustomValueObjectSerializerViaDelegates
- Bug: 0001, 0017, 0092, 0128, 0129
- Unit: AggregateRootTests, ApplicationTests, DefaultTypeAnalyzerServiceTests, natural key serializer tests

Persistence (`tests/dddlib.Persistence.Tests`):

- MemoryEventPersistence and SqlServerEventPersistence, each: UndefinedNaturalKey, UndefinedUnititializedFactory, NullNaturalKey,
  SaveAndLoad, SaveAndSaveAndLoad, SaveAndLoadAndSaveAndLoad, SnapshotAndLoad, SnapshotAndSaveAndLoad, SaveAndEndLifecycleAndSaveAndCreate
- MemoryMementoPersistence: DefaultMemoryPersistence; SqlServerMementoPersistence: DefaultSqlServerPersistence,
  DefaultMementoRepositoryPersistence
- Integration: MemoryEventStoreTests, SqlServerEventStoreTests, SqlServerIdentityMapTests, SqlServerNaturalKeyRepositoryTests,
  SqlServerSnapshotStoreTests
- Bug: 0043, 0064, 0081, 0109, 0127
- Unit: JsonSerializerTests

Shared model (`tests/dddlib.Tests.Support`): Vehicle, Registration, Wheel, NewVehicle, IRegistrationService, Bootstrapper.

## 8. Open questions for Cameron

Answered so far:

- Existing databases: none need to be supported. No compatibility constraints on JSON, type names or namespaces.
- Package identity: publish under the existing `dddlib` package id as 2.0.
- Memento-based `IRepository<T>`: kept. It was dropped on 2026-09-29 and reinstated the same day because it has
  a use case. Async like the rest of persistence; `MementoResult` replaces the out parameters. As in v1 it stores
  only the memento; storing the uncommitted events for dispatch as well is tracked in dddlib/dddlib#149.

- `ValueObject<T>`: kept, with the legacy constraint `where T : ValueObject<T>`. See section 4.

Nothing is open.

Documentation lives in `docs/` (ported from the v1 wiki on 2026-09-29, with the persistence pages the wiki
never had). The runtime `HelpLink` URLs still point at the v1 wiki pages; repoint them once the v2 repository
has a public home for `docs/`.

## 9. Working rules for the implementing session

- Red, green, refactor. Port a scenario, watch it fail, port the code, watch it pass, commit.
- Never delete or skip a scenario to get green. If a scenario cannot be ported yet, leave it with
  `[Skip("reason")]` and list it in the commit message.
- Consult the legacy source for behaviour, error message text and help links. Do not copy the
  StyleCop suppressions, the Guardian guards, or the `JavaScriptSerializer` code.
- Keep commits scoped to one phase step. Prefix messages with the phase, for example `P1: port EntityEquality scenarios`.
- Run `dotnet test` before every commit. SQL Server tests need Docker running.
