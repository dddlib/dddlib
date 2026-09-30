# SQL Server

The SQL Server implementations are in the **dddlib.Persistence.SqlServer** package; the event dispatcher's are in
**dddlib.Persistence.EventDispatcher.SqlServer**.

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
and a failure leaves nothing behind. A dedicated schema such as `dddlib` keeps dddlib's tables and procedures apart
from yours; the constructors default to `dbo`.

The schema is one series of numbered scripts, `dddlib01.sql`, `dddlib02.sql` and so on, the same in every SQL Server
package: installing through `SqlServerEventDispatcherSchema.EnsureAsync` installs the event store too. Version 1
creates:

| Objects | Used by |
|---|---|
| `Types` table and procedures | everything |
| `NaturalKeys` table and procedures | identity map (both models) |
| `Streams`, `Events`, the `EventList` table type, the `SequenceNumber` sequence and procedures | event store, and the memento repositories, which append the events of a save |
| `Snapshots` table and procedures | snapshots |
| `Mementos` table and procedures, including `AppendEvents` | memento repositories |
| `Batches`, `DispatchedEvents` and procedures | [event dispatcher](event-dispatcher.md) |
| `Versions` table | version tracking |

Released scripts never change; every later change to the schema is a new script, so upgrading the packages and
calling `EnsureAsync` upgrades the schema. The `Versions` table holds one row per applied version with the package
that applied it and the text it ran.

### Running the scripts yourself

To run the DDL through a migration tool such as DbUp, or hand it to a DBA, `SqlServerSchema.GetScript(schema)` returns
the whole series rewritten for the schema, preceded by a batch that creates the schema, with batches separated by
`GO`. The scripts also ship under `content/Scripts` in both packages; they target `dbo`, so replace the bracketed
schema name `[dbo]` with yours and run them in version order. Every script records its own version, and version 1 is
idempotent, so `EnsureAsync` later adopts a database installed either way.

Batches are separated by a line holding only `GO` (optionally followed by a `--` comment). `GO` with a repeat count
is not supported.

### Rolling upgrades

Code works against a database at its own version or newer, so processes can be upgraded one at a time. With two
processes A and B at version 2, A restarts on version 3 and upgrades the database to 3; B keeps running on version 2
against it, and can restart on version 2 and call `EnsureAsync` again:

| Code | Database | Result |
|---|---|---|
| same as the database | | works |
| behind the database | ahead | works, and warns |
| ahead of the database | behind | fails |

Before its first command, each SQL Server class reads the schema version once per connection string and schema. If
the schema is older than the package requires, or has no `Versions` table, the call fails with a
`PersistenceException` naming the versions and how to fix it, rather than with a SQL error about a missing procedure.

If the database is newer than the package, `EnsureAsync` applies nothing and returns a `SqlServerSchemaVersion` with
`IsDatabaseAhead` set, and the `SqlServerSchema.DatabaseAhead` event is raised, by `EnsureAsync` and by the first
command per connection string and schema. dddlib has no logging dependency; subscribe and log it:

```csharp
SqlServerSchema.DatabaseAhead += (_, e) => logger.LogWarning(
    "Schema {Schema} is at version {DatabaseVersion}; this process is at {CodeVersion} and should be upgraded.",
    e.Version.Schema, e.Version.DatabaseVersion, e.Version.CodeVersion);
```

`SqlServerEventDispatcherSchema` has the same `EnsureAsync` result and `DatabaseAhead` event with its own types.

This only holds because every script is written expand-then-contract: script N+1 must keep code N working.

- Allowed: new tables, new nullable or defaulted columns, new indexes, new procedures.
- Not allowed: changing the parameters or result columns of a procedure that code N calls, renaming or dropping
  anything code N uses, adding a column code N's inserts cannot satisfy, or tightening a constraint code N can
  violate. To change a procedure's contract, add a new procedure and have the new code call it.
- Removing what code N used happens in a later script, once no process can still be running code N.
- A changed procedure body with the same contract reaches running processes on older code as soon as it is
  applied, so it must keep the behaviour they expect.

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
