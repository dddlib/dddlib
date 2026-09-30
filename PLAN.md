# dddlib v2 port plan

This repository is a ground-up port of [dddlib v1](https://github.com/dddlib/dddlibv1) to modern .NET.
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
`dddlib.Persistence.EventDispatcher` was ported in phase 6. Its SQL Server implementation polls the event store
rather than use `SqlDependency`, which Azure SQL does not support.

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
| Schema setup | Changed 2026-09-29 for dddlib/dddlib#43: dddlib provides and upgrades its own schema. One linear series of numbered scripts (`dddlib01.sql`, ...) shared by every SQL Server package, applied explicitly by `SqlServerSchema.EnsureAsync` and recorded in the schema's `Versions` table, as Meld did in v1 but explicit and async. The scripts still ship as content and record their own version when run by hand. No Meld, no ILMerge. |
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
    dddlib.Persistence/              Sdk abstractions, Memory implementations
    dddlib.Persistence.SqlServer/    SqlServer implementations, SqlServerSchema
    dddlib.Persistence.EventDispatcher/            dispatcher host, Memory implementation
    dddlib.Persistence.EventDispatcher.SqlServer/  SqlServer batch store and host, SqlServerEventDispatcherSchema
    Shared/SqlServer/                SQL Server infrastructure and Scripts/, linked into both SqlServer packages
    dddlib.Generators/               source generator + analyzers (phases 4 and 7)
    dddlib.CodeFixes/                code fixes for the analyzers (phase 7), packed into the dddlib package
  tests/
    dddlib.Tests/                    core feature scenarios, bug regressions, unit tests
    dddlib.Persistence.Tests/        persistence scenarios, integration tests
    dddlib.Generators.Tests/         generator, analyzer and bootstrapper model tests, driving Roslyn directly
    dddlib.CodeFixes.Tests/          each code fix applied in an AdhocWorkspace
    dddlib.Tests.Support/            shared domain model (Vehicle, Registration, Wheel), test bootstrapper helpers, SQL container fixture
```

SQL Server was split out of dddlib.Persistence and dddlib.Persistence.EventDispatcher on 2026-09-29
(dddlib/dddlib#43) so the core packages do not depend on SqlClient; the namespaces are unchanged. The two SQL Server
packages do not reference each other: the schema installer, its scripts and the small internal helpers are compiled
into both as linked files from `src/Shared/SqlServer`.

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
- Nothing in the library changes the schema implicitly. `SqlServerSchema.EnsureAsync` (or
  `SqlServerEventDispatcherSchema.EnsureAsync`) creates or upgrades it when the consumer calls it; the test fixture
  calls it on each per-class database. Each SQL Server class checks the schema version before its first command and
  fails with a `PersistenceException` when the schema is behind, or has recorded that it no longer supports the
  package. Constructors must not do I/O.
- `TransactionScopeOption.Suppress` wrapping is kept so callers' ambient transactions do not leak in.

### Source generators and analyzers (phase 4)

Tracked in dddlib/dddlib#2 (Roslyn analyzer for Visual Studio); the remaining coverage is planned in phase 7.

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
  and the SQL Server parts of Bug0109 plus Bug0127. UpgradeDatabaseVersionTests was dropped while schema setup
  was manual; its intent returned with phase 8 as SqlServerSchemaTests.

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
- Issue dddlib/dddlib#1 (events on the memento path) followed on 2026-09-29: `Repository<T>` hands the uncommitted
  events to its storage method, `SaveMemento` appends them in the memento's transaction through a new `AppendEvents`
  procedure in script 05, `SqlServerRepository<T>.AppendEventsAsync` does the same for custom storage inside the
  caller's `SqlTransaction`, and `MemoryRepository<T>` appends to the `MemoryEventStore` it is composed with. The
  memento's state token is authoritative for concurrency; the stream takes it whenever events are appended.

Done 2026-09-29. What was built, and where it departs from v1:

- `IEventDispatcher.DispatchAsync(sequenceNumber, event, token)` and `CustomEventDispatcher` (delegate) are what
  users implement, as in v1. `Sdk.EventDispatcher` is the polling host over `Sdk.IEventBatchStore`
  (`GetNextBatchAsync` / `MarkDispatchedAsync`); `MemoryEventDispatcher` and `SqlServerEventDispatcher` compose it
  with `MemoryEventBatchStore` and `SqlServerEventBatchStore`. `EventDispatcherOptions` holds dispatcher id, batch
  size, polling interval with doubling backoff to a maximum, and the batch timeout. `RunAsync(token)` is the hosted
  shape; `Start`/`StopAsync`/`DisposeAsync` for everything else. A throwing dispatcher raises `DispatchFailed`,
  abandons the batch and waits the batch timeout so the batch is retried in order.
- `MemoryEventStore` keeps a store-wide sequenced log and exposes `ReadEventsAsync(after, max)`; `SequencedEvent`
  is in `dddlib.Persistence.Sdk`.
- SQL: one script, `06-SqlServerEventDispatcher.sql`, with `Batches` (first and last sequence number per batch,
  UTC timestamp, complete flag), `DispatchedEvents` as one high-water-mark row per dispatcher instead of one row per
  event, and `GetNextBatch` / `MarkDispatched`. Script 03 now also takes a store-wide application lock
  (`dddlib.Events.Commit`) while assigning sequence numbers so they reflect commit order; without it a dispatcher
  could pass a number whose commit was still in flight.
- Tests (16): MemoryEventDispatcher.CanDispatch, SqlServerEventDispatcher.CanDispatch, SqlServerEventStoreTests (7,
  `[NotInParallel]` because the batch feed is store-wide), MemoryEventBatchStoreTests (6, using a fake
  `TimeProvider` for the timeout and a dispatcher retry-in-order case), public API snapshot.


### Phase 7: analyzer coverage (dddlib/dddlib#2)

Planned 2026-09-29, done 2026-09-30 on the `analyzers` branch; see *As built* at the end of this phase. Turns the remaining runtime-only model mistakes into diagnostics, adds the code fixes
the issue lists, and refines DDDLIB004. Everything stays inside the `dddlib` package: the analyzers in
`dddlib.Generators`, the code fixes in a new `dddlib.CodeFixes` assembly packed into the same `analyzers/dotnet/cs`
folder. Roslyn stays at 4.14 (.NET 9.0.300 SDK, Visual Studio 17.14).

Constraints that shape the work:

- `EnforceExtendedAnalyzerRules` is on, and RS1038 forbids a compiler-loaded analyzer assembly from referencing
  `Microsoft.CodeAnalysis.Workspaces`. Code fix providers therefore go in `src/dddlib.CodeFixes` (netstandard2.0,
  references `dddlib.Generators` and `Microsoft.CodeAnalysis.CSharp.Workspaces` 4.14 with `PrivateAssets="all"`).
  `dddlib.csproj` packs both DLLs.
- `TreatWarningsAsErrors` is on repo-wide and every test project references the generator as an analyzer, so a new
  rule that fires on the test models fails the build. Each rule lands only when the whole solution builds; a hit in
  a test model is either a test model fix or a false positive to fix in the rule, never a suppression. The exception,
  as for DDDLIB001 and DDDLIB004 before: code that breaks a rule on purpose carries a scoped `#pragma` with the reason
  (the double-dispatch probe in the shared `Vehicle`, the benchmark subjects that must not record events).
- Every rule keeps the existing style: `DiagnosticDescriptors` entry with a `helpLinkUri` into `docs/`, a row in
  `AnalyzerReleases.Unshipped.md`, a row in the table in `docs/source-generator.md`, a sentence on the feature page
  it relates to, and a red and a green test in `tests/dddlib.Generators.Tests`.

#### 7.0 Shared analysis (do first)

1. `BootstrapperModel`, in `src/dddlib.Generators/BootstrapperModel.cs`: a per-compilation, lazily built record of what
   the assembly's bootstrapper configures. Built once from the single `IBootstrapper` implementation by walking the
   `Bootstrap` method with the semantic model: every fluent chain rooted at `configure.AggregateRoot<T>()`,
   `configure.Entity<T>()` or `configure.ValueObject<T>()` yields, per `T`, the set of `ToReconstituteUsing`,
   `ToUseNaturalKey` (with the selected property symbol when the lambda body is a member access, otherwise a marker),
   `ToUseEqualityComparer`, `ToUseValueObjectSerializer` and `ToMapToEvent<TEvent>` (with a flag for the reverse
   mapping overload) calls. The model is `Unknown` when there is more than one bootstrapper, the `configure`
   parameter is used anywhere other than as the receiver of one of those three methods (helpers, loops, assignments),
   a wrapper leaves its chain (stored in a variable, passed on), or any other method in the compilation takes an
   `IConfiguration`. The last was added while implementing: a custom `IBootstrapperProvider` can hand the
   configuration to classes that are not the bootstrapper, as the nested `IBootstrap<T>` classes of the feature
   scenarios do, and the single `IBootstrapper` of dddlib.Tests would otherwise make every such scenario look
   unconfigured. With that trigger in place, an assembly with no bootstrapper is known to configure nothing (changed
   in 7.2 from the original plan, which made it `Unknown`): the default provider only looks in the type's own
   assembly, and otherwise DDDLIB014 and DDDLIB015 would stay silent for exactly the model that has no bootstrapper
   yet.
   Bootstrapper-aware rules do not report against an `Unknown` model. Exposed through a `Lazy<BootstrapperModel>`
   (`BootstrapperModel.GetLazy`, one per compilation) shared by all analyzers, since symbol actions run concurrently.
   The generator's pipeline record that had the name is now `BootstrapperRegistration`.
   Tests: `BootstrapperModelTests` covering each call kind, the reverse-mapping flag, the member-access marker, and
   each `Unknown` trigger.
2. `KnownSymbols` gains the symbols the new rules need: `IConfiguration` and the three wrapper interfaces,
   `IMapperProvider` and the three mapper interfaces, `AggregateRoot.Apply`, `GetState`, `SetState`, `BusinessException`.
3. `SymbolExtensions` gains `HasValueEquality(ITypeSymbol)` (string, primitives, enums, structs, value objects,
   types overriding `Equals(object)` or implementing `IEquatable<T>`) and `IsDefaultSerializable(INamedTypeSymbol)`
   (a public parameterless constructor with every public property settable or init-able, or exactly one public
   constructor whose parameters match the public properties by name, case-insensitively) with a result naming the
   offending property, reused by DDDLIB012 and DDDLIB017. As built: `[JsonConstructor]`, `[JsonIgnore]` and
   `[JsonInclude]` are honoured, a computed property (no setter, no backing field) is not an offender, and a
   constructor parameter that matches no property is reported separately (`UnboundParameter`).

#### 7.1 Event application rules (new `EventApplicationAnalyzer`, operation actions)

| Id | Severity | Reports when | Detection |
|---|---|---|---|
| DDDLIB008 | Warning | `Apply(x)` in an aggregate root where no non-public `Handle` with exactly the static type of `x` exists anywhere in the containing type's hierarchy | `IInvocationOperation` on `AggregateRoot.Apply`; argument static type must be a concrete class (skip `object`, abstract, type parameters); handlers collected with `GetDispatchableHandlers` up the base chain, metadata types included |
| DDDLIB009 | Warning | A `Handle` method's parameter is an abstract class or an interface, which exact-type dispatch never matches | Symbol action, extends the DDDLIB002 branch in `DomainTypeAnalyzer`; type parameters stay exempt |
| DDDLIB010 | Warning | `Apply` is invoked inside a `Handle` method (directly, including lambdas within it) | `IInvocationOperation` whose containing method is a dispatchable handler |
| DDDLIB011 | Warning | A `Handle` method contains a `throw` statement or expression | `IThrowOperation` whose containing method is a dispatchable handler; message says handlers run on replay |
| DDDLIB012 | Warning | A public get-only property on an event or memento type has no constructor parameter of the same name, so it is written but not loaded | Event types are the static types of `Apply` arguments and handler parameters; memento types are the types of `new` expressions returned from `GetState` overrides. Each type analyzed once per compilation (`ConcurrentDictionary`), only when declared in the compilation; reported at the property. A registered `JsonSerializerContext` follows the same rules, so no exemption |

Red and green tests per rule, plus for DDDLIB008: a handler declared on a base class in metadata (compile the base
into a reference assembly in the test) and an argument typed as a base class of the handled type (green, since the
runtime type is unknown).

#### 7.2 Type shape rules (extend `DomainTypeAnalyzer`, symbol actions)

| Id | Severity | Reports when | Detection |
|---|---|---|---|
| DDDLIB013 | Warning | An aggregate root overrides exactly one of `GetState` and `SetState` | `GetMembers` of the type, overrides only |
| DDDLIB014 | Warning | A non-abstract aggregate root has no parameterless constructor of any accessibility and the bootstrapper model has no `ToReconstituteUsing` for it | `HasParameterlessConstructor` plus `BootstrapperModel`; message names both fixes and that applied events are not recorded without a factory |
| DDDLIB015 | Warning | A non-abstract aggregate root has no `[NaturalKey]` anywhere in its hierarchy and the bootstrapper model has no `ToUseNaturalKey` for it or a base type | Walk base types for declared natural keys; entities are deliberately not reported (an entity without a key is legal) |
| DDDLIB016 | Error | `[NaturalKey]` on a property the runtime ignores: non-public getter, static, indexer, write-only, or on a type that is not an entity | `GetAttributes` on every property symbol, compared with the filter in `GetDeclaredNaturalKeyProperties` |
| DDDLIB017 | Warning | A natural key type cannot round-trip: a class type other than `string` without value equality, or a value object that is not default-serializable and has no `ToUseValueObjectSerializer` in the bootstrapper model | Applies to the effective natural key of aggregate roots (attribute or bootstrapper); uses `HasValueEquality` and `IsDefaultSerializable`; help link to value-object-serialization.md, as the runtime message |
| DDDLIB018 | Error | `class A : ValueObject<B>` where `B` is not `A` | Compare `GetValueObjectArgument` with the type; generic self-types allowed |
| DDDLIB019 | Warning | A public property of a value object has a class type other than `string` that is not enumerable, not a value object, and has no value equality | `GetValueObjectProperties` plus `HasValueEquality` |
| DDDLIB004 | (refine) | Not reported when the bootstrapper model has `ToUseEqualityComparer` for the type | Consult the model; the docs sentence about suppressing it goes |

#### 7.3 Bootstrapper rules (extend `BootstrapperAnalyzer`)

| Id | Severity | Reports when | Detection |
|---|---|---|---|
| DDDLIB020 | Warning | `Map.Entity(x).ToEvent<E>()`, `Map.ValueObject(v).ToEvent<E>()`, `Map.Event(e).ToEntity<T>()` or `Map.Event(e).ToValueObject<T>()` with no matching `ToMapToEvent<E>` for `T` in the bootstrapper model; the two reverse forms also require the reverse-mapping overload | `IInvocationOperation` on the mapper interface methods; `T` from the receiver's type argument, `E` from the method's; reported at the call site |
| DDDLIB021 | Error | The bootstrapper's `ToUseNaturalKey` selects a different property from the `[NaturalKey]` declared on the same type | `BootstrapperModel` against `GetDeclaredNaturalKeyProperties` |
| DDDLIB022 | Error | A `ToUseNaturalKey` selector whose body is not a member access on the parameter | Syntax of the lambda argument; the runtime throws `ArgumentException` at bootstrap otherwise |

#### 7.4 Code fixes (new `src/dddlib.CodeFixes`, new `tests/dddlib.CodeFixes.Tests`)

In order of value. Each test applies the fix through an `AdhocWorkspace` and compares the resulting source.

1. DDDLIB014: add `protected internal Type() { }` with a "used for reconstitution only" comment, after the last
   constructor or as the first member.
2. DDDLIB007: add `partial` to the type and every non-partial containing type, across all declarations.
3. DDDLIB003: change the handler's accessibility to `private`.
4. DDDLIB008: add `private void Handle(EventType @event) { }` after the last existing handler.
5. DDDLIB013: add the missing `GetState` (returning `null`) or `SetState` (`throw new NotImplementedException()`)
   override with a comment pointing at aggregate-root-mementos.md.
6. DDDLIB001: remove the `[NaturalKey]` attribute, one fix registered per attribute so the author chooses.

#### 7.5 Landing order and exit

Commits, each green on the whole solution: `P7.0` shared analysis and model tests; `P7.1` one commit per rule or
small group; `P7.2`, `P7.3` likewise; `P7.4` the code fix project and packaging; `P7.5` docs (the diagnostics table,
feature pages, `docs/bootstrapper.md` on what the analyzer can and cannot read from a bootstrapper) and
`RELEASE_NOTES.md`. The runtime checks stay for non-generated and dynamically configured models.

Exit: DDDLIB008 to DDDLIB022 and the six code fixes shipped in the `dddlib` package; every runtime "To fix this
issue" message that is statically decidable has a diagnostic; issue #2 closed with the table in
`docs/source-generator.md` as the record.

As built, where it departs from the plan above:

- No bootstrapper means a known, empty model rather than `Unknown`, and any other method taking an `IConfiguration`
  makes the model `Unknown` (both described in 7.0). The feature test projects configure through nested
  `IBootstrap<T>` classes, so the bootstrapper-aware rules never fire there; the rules are covered by
  `tests/dddlib.Generators.Tests` alone.
- DDDLIB008 is not reported when a base class of the aggregate root is in a referenced assembly: the compiler does
  not import private members from metadata, so the planned "metadata types included" cannot see the handlers. With an
  argument that is not a `new` expression, a handler for a derived type also counts.
- DDDLIB013 looks at the whole hierarchy of a non-abstract aggregate root, so an abstract base and its subclass may
  supply one override each.
- DDDLIB015 accepts `ToUseNaturalKey` for a base type as planned, although the runtime applies bootstrapper
  configuration to the exact type only; the rule errs on the side of silence.
- DDDLIB017 and DDDLIB019 skip properties typed `object`, whose equality is not known until runtime. DDDLIB019 is not
  reported when the bootstrapper configures a comparer, like DDDLIB004.
- DDDLIB020 accepts a mapping configured for a type derived from the one at the call site (forward) or for an event
  derived from the one mapped back (reverse), because the runtime looks mappings up by runtime type.
- The code fixes place and indent what they add themselves instead of running the formatter. The DDDLIB007 fix stays
  within the document: a type that is not partial has one declaration, which contains everything nested in it. The
  DDDLIB014 fix writes `private` rather than `protected internal` on a sealed type.
- Test models that broke a rule by accident were fixed (four applied events without handlers). Three break one on
  purpose and carry a scoped pragma: the double-dispatch probe in `Vehicle` (DDDLIB011), the benchmark subjects
  (DDDLIB014) and Bug0064 (DDDLIB017).
- Not verified here: the code fixes loading in Visual Studio from the packed `analyzers/dotnet/cs` folder. The
  package contents were checked (`dddlib.Generators.dll` and `dddlib.CodeFixes.dll`), the editor was not.

Added 2026-09-30 for dddlib/dddlib#48: DDDLIB023 (warning, `EventApplicationAnalyzer`), an event or memento that is
saved but fails to load. `IsDefaultSerializable` already told these cases apart and DDDLIB012 dropped them: no
constructor the serializer can use (several public ones with none parameterless or marked `[JsonConstructor]`, or
none public), and a constructor parameter that no property matches. While `Apply<T>` required `new()` the first could
not happen to an applied event; mementos never had that guard. Reported at the type, not for abstract types, and like
DDDLIB012 only for types declared in the compilation.

Considered and left out: a handler whose event is never applied (events arrive from subclasses and mappings, so it
is noisy), publicly settable value object properties (a shape the serialization docs sanction), a public bootstrapper
(only a recommendation), and reporting DDDLIB015 for entities.

### Phase 8: dddlib provides and upgrades its own SQL Server schema (dddlib/dddlib#43)

Done 2026-09-29, in three commits, each green:

1. Package split: `dddlib.Persistence.SqlServer` and `dddlib.Persistence.EventDispatcher.SqlServer`, with the shared
   internals linked from `src/Shared/SqlServer`. `GetNextBatch` returns the event type name so the dispatcher
   resolves types through `TypeNameResolver` and needs no `SqlServerTypeCache` (which stays public in
   dddlib.Persistence.SqlServer).
2. One script series and `EnsureAsync`. Scripts 01-06 merged into `dddlib01.sql`, idempotent so it adopts a database
   installed before versioning. Kept from Meld: numbered embedded scripts starting at 1 and contiguous, a `Versions`
   row per applied script with the package description and the applied text. (Meld also refused a database that
   is ahead; step 4 dropped that for rolling upgrades.)
   Changed: explicit and async, the `Versions` table in the named schema, one transaction for the whole upgrade under
   an exclusive `sp_getapplock` on the schema, no AppDomain scanning, no server-version directives, `PersistenceException`
   instead of a forged `SqlException`, and (from step 4) a database ahead is reported rather than refused.
   `GetScript(schema)` returns the whole series for migration tools. Batches are split on `GO` lines; `GO <count>` is
   rejected.
3. Fail loudly when behind: `SqlServerSchemaCheck` reads the version once per connection string and schema before
   the first command of every SQL Server class and caches only a compatible schema.
4. Rolling upgrades (2026-09-30): a schema ahead of the package is accepted instead of failing, so a process on older
   code keeps working after a newer one upgrades the schema. `EnsureAsync` returns a version record
   (`SqlServerSchemaVersion`, `SqlServerEventDispatcherSchemaVersion`, one per SQL Server package, not in
   dddlib.Persistence, whose API has no SQL Server concepts) with `IsAhead`; the caller decides whether to warn.
   There is no event: everything is evaluated from the returned version.
5. Review follow-ups (2026-09-30):
   - Vocabulary: the record is `(Schema, Version, RequiredVersion, MinimumRequiredVersion)` with `IsAhead` and
     `IsCompatible`, matching "the schema is at version X, the package requires version Y" in the exception text.
     Versions are per schema, not per database.
   - A bound on "ahead": the `Versions` table has a nullable `MinimumRequiredVersion`. A contracting script sets it on
     its own row to the oldest required version that still works; the schema check and `EnsureAsync` throw a
     `PersistenceException` for a package that requires less. It had to be in `dddlib01.sql` and in the first release,
     because a package that does not read the column can never be told it is too old. Meld had no equivalent.
   - `GetVersionAsync` on both schema classes returns the same record from a read alone and never throws for an
     incompatible schema, for processes that leave the DDL to a migration tool and so never call `EnsureAsync`.
   - `EnsureAsync` reads the versions first and takes the upgrade lock only when there is something to apply or a
     script text to fill in, so instance startups do not queue behind an upgrade.

Superseded from the issue: "only what is used". The whole schema is one series, so installing the dispatcher
installs the event store.

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
- AggregateRootEventApplication: EventsAreStoredOnAggregate, EventsAreStoredOnInheritedAggregate, InheritedEventsAreStoredOnInheritedAggregate,
  PositionalRecordEventsAreStoredOnAggregate (dddlib/dddlib#48: `Apply<T>` is constrained to `class` only; v1's `new()`
  served `JavaScriptSerializer`, and System.Text.Json reads a positional record through its primary constructor)
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
  SaveAndLoad, SaveAndSaveAndLoad, SaveAndLoadAndSaveAndLoad, SnapshotAndLoad, SnapshotAndSaveAndLoad, SaveAndEndLifecycleAndSaveAndCreate,
  SaveAndLoadWithPositionalRecordEvents
- MemoryMementoPersistence: DefaultMemoryPersistence, EventsAreStoredForDispatch; SqlServerMementoPersistence: DefaultSqlServerPersistence,
  DefaultMementoRepositoryPersistence, EventsAreStoredForDispatch, CustomStorageStoresEvents, CustomIdentityMap
  (dddlib/dddlib#45: `SqlServerRepository<T>` has a second constructor taking an `IIdentityMap`)
- Integration: MemoryEventStoreTests, SqlServerEventStoreTests, SqlServerIdentityMapTests, SqlServerNaturalKeyRepositoryTests,
  SqlServerSnapshotStoreTests, SqlServerSchemaTests (CreatesTheSchemaWithEveryObject, EnsuringTwiceChangesNothing,
  EnsuringACurrentSchemaDoesNotWaitForTheUpgradeLock, UpgradeAppliesOnlyTheMissingVersion,
  FailedUpgradeRollsBackEntirely, FailedInstallLeavesNoSchema, ConcurrentCallersApplyEachVersionOnce,
  ReportsTheVersionOfACurrentSchema, ReportsWhenTheSchemaIsAheadOfThePackage,
  OlderCodeKeepsWorkingAfterNewerCodeUpgrades, OlderCodeFailsLoudlyAfterAContractingUpgrade,
  GetVersionReadsWithoutChangingAnything, GetVersionReportsASchemaThatIsAheadOrNoLongerSupportsThePackage,
  AdoptsAScriptRunByHand, GetScriptInstallsTheSchema, FailsLoudlyWhenTheSchemaIsBehind,
  FailsLoudlyWhenTheVersionIsMissing, RejectsAnInvalidSchemaName)
- Bug: 0043, 0064, 0081, 0109, 0127
- Unit: JsonSerializerTests, SqlServerScriptTests

Event dispatcher (`tests/dddlib.Persistence.EventDispatcher.Tests`):

- MemoryEventDispatcher and SqlServerEventDispatcher, each: CanDispatch, CanDispatchFromMementoRepository,
  CanDispatchPositionalRecordEvent
- Integration: SqlServerEventStoreTests (TryGetBatchFromEmptyEventStore, TryGetBatchFromEventStoreWithSingleEvent,
  TryGetBatchTwiceFromEventStoreWithSingleEvent, TryGetBatchTwiceFromEventStoreWithSingleEventAndDifferentDispatchers,
  TryGetMultipleBatchesFromEventStoreWithManyEvents, MarkingDispatchedCompletesTheBatchAndAdvances,
  AnAbandonedBatchIsHandedOutAgainAfterTheTimeout), MemoryEventBatchStoreTests, SqlServerEventDispatcherSchemaTests
  (InstallingTheDispatcherInstallsTheEventStore, ReportsWhenTheSchemaIsAheadOfThePackage,
  GetVersionReadsWithoutChangingAnything, FailsLoudlyWhenThePackageIsTooOldForTheSchema,
  FailsLoudlyWhenTheSchemaIsBehind, BothPackagesProduceTheSameScript)

Shared model (`tests/dddlib.Tests.Support`): Vehicle, Registration, Wheel, NewVehicle, IRegistrationService, Bootstrapper.

## 8. Open questions for Cameron

Answered so far:

- Existing databases: none need to be supported. No compatibility constraints on JSON, type names or namespaces.
- Package identity: publish under the existing `dddlib` package id as 2.0.
- Memento-based `IRepository<T>`: kept. It was dropped on 2026-09-29 and reinstated the same day because it has
  a use case. Async like the rest of persistence; `MementoResult` replaces the out parameters. As in v1 it stores
  only the memento; since 2026-09-29 it also appends the uncommitted events to the aggregate root's stream in the same
  transaction so the event dispatcher delivers them (dddlib/dddlib#1).

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
- Released SQL Server schema scripts never change. Any schema change is a new `dddlibNN.sql` in `src/Shared/SqlServer/Scripts`
  that ends by recording its own version, like `dddlib01.sql`.
- Code must keep working against a database ahead of it (rolling upgrades: a process on version N runs against a
  database a newer process upgraded to N+1). Code ahead of the database throws; a database ahead of the code is
  reported by `EnsureAsync` and `GetVersionAsync` through `IsAhead` on the returned version. So every script is
  expand-then-contract:
  - Expand: only additive changes (new tables, nullable or defaulted columns, indexes, new procedures). Never change
    the parameters or result columns of a procedure older code calls, rename or drop what it uses, add a column its
    inserts cannot satisfy, or tighten a constraint it can violate, in the same version that stops using it. To change
    a procedure's contract, add a new procedure and have the new code call it. A changed procedure body with the same
    contract reaches running processes on older code as soon as it is applied, so it must keep the behaviour they
    expect.
  - Contract: removing what code N used happens in a later script, which must record the oldest required version that
    still works: `INSERT INTO [dbo].[Versions] ([Version], [MinimumRequiredVersion]) VALUES (NN, M);`. Older packages
    then fail loudly instead of with a SQL error. Say so in `RELEASE_NOTES.md`; docs/persistence/sql-server.md,
    *Rolling upgrades*, promises that.
