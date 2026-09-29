| `06-SqlServerEventDispatcher.sql` | `Batches`, `DispatchedEvents` and procedures | [event dispatcher](event-dispatcher.md); shipped in **dddlib.Persistence.EventDispatcher** |
# SQL Server

## Schema

The SQL Server implementations expect their schema to exist. The scripts are shipped in the **dddlib.Persistence**
package under `content/Scripts`, one per component, and must be run in order before first use:

| Script | Creates | Needed by |
|---|---|---|
| `01-SqlServerPersistence.sql` | `Types` table and procedures | everything |
| `02-SqlServerNaturalKey.sql` | `NaturalKeys` table and procedures | identity map (both models) |
| `03-SqlServerEventStore.sql` | `Streams`, `Events`, the `EventList` table type, the `SequenceNumber` sequence and procedures | event store |
| `04-SqlServerSnapshotStore.sql` | `Snapshots` table and procedures | snapshots |
| `05-SqlServerMementoRepository.sql` | `Mementos` table and procedures, including `AppendEvents` | memento repository; needs script 03 because saves append events |

The scripts are idempotent (`CREATE OR ALTER`, guarded table creation), so they can be rerun. They target the `dbo`
schema; to use another schema, replace `[dbo]` in the scripts and pass the schema name to the constructors. The same
text is available programmatically through `SqlServerScripts.Read(name, schema)`, together with
`SqlServerScripts.SplitBatches` for running them batch by batch, which is what the test fixture does.

Nothing in the library creates or migrates the schema at runtime, and there is no compatibility with v1 databases.

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
