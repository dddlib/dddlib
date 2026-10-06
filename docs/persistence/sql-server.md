# SQL Server

The SQL Server implementations are in the **dddlib.Persistence.SqlServer** package; the event dispatcher's are in
**dddlib.Persistence.EventDispatcher.SqlServer** and the projections' in **dddlib.Persistence.Projections.SqlServer**.

```shell
dotnet add package dddlib.Persistence.SqlServer
```

## Schema

dddlib owns its schema and versions it. Nothing in the library changes the schema implicitly and constructors do no
I/O: create or upgrade it explicitly, from a migration step or at startup, under a credential allowed to run DDL:

```csharp
using dddlib.Persistence.SqlServer;

await SqlServerSchema.EnsureAsync(connectionString, "dddlib", cancellationToken);

var repository = new SqlServerEventStoreRepository(connectionString, "dddlib");
```

`EnsureAsync` creates the schema if it does not exist and applies the versions it is missing, in order, in one
transaction under an exclusive application lock on the schema, so it is safe to call from every instance at startup
and a failure leaves nothing behind. When there is nothing to apply it only reads the versions and takes no lock. A
dedicated schema such as `dddlib` keeps dddlib's tables and procedures apart
from yours; the constructors default to `dbo`.

The schema is one series of numbered scripts, `dddlib01.sql`, `dddlib02.sql` and so on, the same in every SQL Server
package: installing through `SqlServerEventDispatcherSchema.EnsureAsync` or `SqlServerProjectionsSchema.EnsureAsync`
installs the event store too. Version 1 creates:

| Objects | Used by |
|---|---|
| `Types` table and procedures | everything |
| `NaturalKeys` table and procedures | identity map (both models) |
| `Streams`, `Events`, the `EventList` table type, the `SequenceNumber` sequence and procedures | event store, and the memento repositories, which append the events of a save |
| `Snapshots` table and procedures | snapshots |
| `Mementos` table and procedures, including `AppendEvents` | memento repositories |
| `Batches`, `DispatchedEvents` and procedures | [event dispatcher](event-dispatcher.md) |
| `Versions` table | version tracking |

Version 2 adds:

| Objects | Used by |
|---|---|
| `ReadEvents`, `GetLastSequenceNumber` and the `TypeNameList` table type | the event feed, which [projections](projections.md) read |
| `Projections`, `ProjectionViews`, the `ProjectionViewList` table type and the projection procedures | projections |

Version 3 adds:

| Objects | Used by |
|---|---|
| The `ValueHash` column of `NaturalKeys` (a hash of the serialized key), the unique index `UX_NaturalKey_Value` on it for keys that are not removed, the index `IX_NaturalKey_Id`, and `GetOrAddNaturalKey` | the identity map, for natural keys [compared by the repository](serialization.md#how-natural-keys-are-compared) |

It also changes the body of `RemoveNaturalKey`, which keeps its parameters and effect but retries a concurrent
conflict itself instead of failing back to the caller.

Released scripts never change; every later change to the schema is a new script, so upgrading the packages and
calling `EnsureAsync` upgrades the schema. The `Versions` table holds one row per applied version with the package
that applied it and the text it ran.

### Running the scripts yourself

To run the DDL through a migration tool such as DbUp, or hand it to a DBA, `SqlServerSchema.GetScript(schema)` returns
the whole series rewritten for the schema, preceded by a batch that creates the schema, with batches separated by
`GO`. The scripts also ship under `content/Scripts` in every SQL Server package; they target `dbo`, so replace the bracketed
schema name `[dbo]` with yours and run them in version order. Every script records its own version, and version 1 is
idempotent, so `EnsureAsync` later adopts a database installed either way.

Batches are separated by a line holding only `GO` (optionally followed by a `--` comment). `GO` with a repeat count
is not supported.

### Schema version check

Before its first command, each SQL Server class reads the schema version once per connection string and schema, and
compares it with the version the package requires, which is the number of its latest script:

| Schema compared with the package | Result |
|---|---|
| at the required version | works |
| newer | works; the returned version has `IsAhead` set |
| older, or without a `Versions` table | fails with a `PersistenceException` |
| newer, and no longer supporting the package | fails with a `PersistenceException` |

The exception names the versions and how to fix it, rather than leaving a SQL error about a missing procedure. A
process whose schema was behind recovers without a restart once the schema has been upgraded.

`EnsureAsync` returns a `SqlServerSchemaVersion`: `Schema`, `Version` (what the schema is at), `RequiredVersion` (what
the package requires), `MinimumRequiredVersion` (the oldest required version the schema still supports), `IsAhead`
and `IsCompatible`. A process that leaves the DDL to a migration tool or a DBA gets the same record from
`SqlServerSchema.GetVersionAsync`, which only reads, and reports a schema the package cannot use through
`IsCompatible` instead of throwing. That suits a startup log or a health check:

```csharp
var version = await SqlServerSchema.GetVersionAsync(connectionString, "dddlib", cancellationToken);
if (!version.IsCompatible)
{
    // The schema is behind this package, or no longer supports it: the first command will fail.
}
else if (version.IsAhead)
{
    logger.LogWarning(
        "Schema {Schema} is at version {Version}; this process requires {RequiredVersion} and should be upgraded.",
        version.Schema, version.Version, version.RequiredVersion);
}
```

`SqlServerEventDispatcherSchema` and `SqlServerProjectionsSchema` have the same methods and return a
`SqlServerEventDispatcherSchemaVersion` and a `SqlServerProjectionsSchemaVersion`. A read model in its own database
needs the schema there too, so call `EnsureAsync` for each database.

### Rolling upgrades

A package works against a schema at the version it requires or newer, so processes can be upgraded one at a time.
With two processes A and B on a package that requires version 2, A restarts on a package that requires version 3 and
upgrades the schema to 3. B keeps running against it, and can restart on its old package and call `EnsureAsync`
again, which applies nothing and returns a version with `IsAhead` set.

How far behind a process may be is recorded in the schema. Scripts only add to the schema until one has to remove or
change something that older packages use. That script records the oldest required version that still works in the
`MinimumRequiredVersion` column of its `Versions` row, and from then on a package that requires less fails the
version check with a `PersistenceException` telling it to upgrade. Until a script does that, every package works
against every later schema. The release notes say when a version raises the minimum.

The check runs once per process, before its first command, so a process that is already running is not told when
the schema stops supporting it. Upgrade every process to at least the new minimum before applying a version that
raises it.

There is no compatibility with v1 databases.

## Requirements

SQL Server 2016 or later, or Azure SQL Database. The scripts use `MERGE`, `SEQUENCE`, `THROW`, table-valued
parameters and `sp_getapplock`, all available there. The client library is `Microsoft.Data.SqlClient`.

## Concurrency and errors

| Error | Meaning | Surfaced as |
|---|---|---|
| 50409 | commit state mismatch | `ConcurrencyException` |
| 50500 | commit lock not acquired within one second | `ConcurrencyException` |
| 1222 | read lock request timed out | `ConcurrencyException` |

Commits take an exclusive application lock on the stream identity for the duration of the transaction, and a
store-wide one (`dddlib.Events.Commit`) while sequence numbers are assigned, so the sequence reflects commit order
for the [event dispatcher](event-dispatcher.md). The memento repository takes the same locks when a save appends
events. Reads hold the
stream row under `HOLDLOCK` inside a short transaction so the state token and the events come from the same committed
version.

## Ambient transactions

All operations run under `TransactionScopeOption.Suppress`, so a caller's ambient `TransactionScope` does not enlist
the persistence connections.

## Connection pooling in tests

When tests drop their database on teardown, clear only that database's connection pool
(`SqlConnection.ClearPool(connection)`); `ClearAllPools` disturbs other tests running in parallel.
