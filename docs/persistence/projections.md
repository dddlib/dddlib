# Projections

The **dddlib.Persistence.Projections** package keeps read models up to date from the events committed to an event
store. A projection reads the store-wide sequence of events after its checkpoint, a page at a time, applies each page
to its read model and commits the read model and the new checkpoint together, so every event takes effect on the read
model exactly once however the process fails.

```shell
dotnet add package dddlib.Persistence.Projections
```

The SQL Server implementations are in **dddlib.Persistence.Projections.SqlServer**:

```shell
dotnet add package dddlib.Persistence.Projections.SqlServer
```

Use a projection when the output is a read model you can rebuild from the events. For side effects that cannot be
rolled back (sending an email, calling another system) use the [event dispatcher](event-dispatcher.md), which
delivers at least once.

## Two kinds

- **Key/value views.** The projection writes views keyed by an identity into a repository that dddlib provides: in
  memory, or the `ProjectionViews` table in dddlib's SQL Server schema, as JSON. Readers get a view by key or list them
  all. This is the quickest way to a read model.
- **Your own tables** (SQL Server). The projection's handlers receive the `SqlTransaction` and write to tables you
  design, index and query; dddlib keeps the checkpoint in the same transaction. Use this when the read model needs
  real queries.

## Writing a projection

Subclass `Projection<TIdentity, TEntity>`, give it a name and register a handler per event type in the constructor.
Handlers receive the event and the views:

```csharp
using dddlib.Persistence.Projections;

public sealed record CarView(string Registration, string? Owner, int Events);

public sealed class CarProjection : Projection<string, CarView>
{
    public CarProjection()
        : base("cars")
    {
        this.When<CarRegistered>((e, views) =>
            views.AddOrUpdateAsync(e.Registration, new CarView(e.Registration, null, 1)));

        this.When<CarSold>(async (e, views, cancellationToken) =>
        {
            var car = await views.GetAsync(e.Registration, cancellationToken) ?? throw new InvalidOperationException();
            await views.AddOrUpdateAsync(e.Registration, car with { Owner = e.Buyer, Events = car.Events + 1 }, cancellationToken);
        });

        this.When<CarScrapped>((e, views) => views.RemoveAsync(e.Registration));
    }
}
```

The name is what the checkpoint and the views are stored under; keep it stable. Dispatch is by exact event type, as
it is for an aggregate root's `Handle` methods. A `When<object>` handler is a catch-all for every event no exact
handler takes. A third overload hands the handler the `FeedEvent` envelope as well, with the sequence number and the
identity of the stream (the aggregate root's identity in the identity map) when the event does not carry the natural
key:

```csharp
this.When<CarRegistered>((envelope, e, views, cancellationToken) =>
    views.AddOrUpdateAsync(envelope.StreamId.ToString(), new CarView(e.Registration, null, 1), cancellationToken));
```

Within a page, the views a handler reads include what earlier handlers in the page wrote, and nothing reaches the
store until the page commits. The last write per key wins, a remove hides the stored view, and `PurgeAsync` from a
handler clears the whole read model.

The projection reads only the event types it handles. Events of other types move its checkpoint along without being
read, resolved or deserialized, so a projection is not stopped by an event type its process does not reference or
that no longer exists; the exception is a projection with a catch-all handler, which reads everything.

## Running it

A `ProjectionRunner` takes an event feed and a projection store. The feed is the event store the repository writes
to (`MemoryEventStore` or `SqlServerEventStore`, both implement `IEventFeed`); the store is where the projection's
views and checkpoint live.

In memory, for tests:

```csharp
using dddlib.Persistence.Projections.Memory;

var store = new MemoryProjectionStore<string, CarView>(new CarProjection());
await using var runner = new ProjectionRunner(eventStore, store);
runner.Start();

var car = await store.Views.GetAsync("W807ASB");
```

On SQL Server:

```csharp
using dddlib.Persistence.Projections.SqlServer;
using dddlib.Persistence.SqlServer;

await SqlServerProjectionsSchema.EnsureAsync(connectionString);

var store = new SqlServerProjectionStore<string, CarView>(connectionString, new CarProjection());
await using var runner = new ProjectionRunner(new SqlServerEventStore(connectionString), store);
await runner.RunAsync(stoppingToken); // until the token is cancelled
```

`RunAsync` is the shape a `BackgroundService` wants; outside a host, `Start()` runs the loop on a background task
and `StopAsync()` (or `DisposeAsync`) stops it after the page in progress. Several runners may share a projection, in
one process or many: they never both apply a page, and the one that loses a race just carries on from the checkpoint
the other left.

Readers anywhere construct the repository with the same connection string and projection name:

```csharp
var cars = new SqlServerRepository<string, CarView>(connectionString, "cars");
var car = await cars.GetAsync("W807ASB");
await foreach (var (registration, view) in cars.GetAllAsync()) { }
```

Keys are the identity serialized as JSON (a string key is stored with its quotes), compared ordinally, and at most
400 characters long. Views are JSON too; see [Serialization](serialization.md) for what that needs of a type.

### Options

`ProjectionRunnerOptions` sets the page size (`BatchSize`, 500 by default, since a page is one transaction and
catch-up is the expensive case), the polling delay after an empty poll (`PollingInterval`, doubling on each empty poll
up to `MaxPollingInterval`) and the delay before a failed page is tried again (`RetryDelay`). The runner takes an
optional `TimeProvider` so tests of the delays need not wait.

### Failures

When a page cannot be read or applied, the runner raises `ProjectionFailed` with the exception, waits `RetryDelay`,
re-reads the checkpoint and tries again, so the page is retried in order and nothing is skipped. A handler that threw
surfaces as a `ProjectionException` naming the projection, the sequence number and the event, with the handler's
exception inside. The runner does not log; subscribe to `ProjectionFailed` to do so. A page that fails changes
nothing: its views are not written and the checkpoint does not move.

### Lag

`GetStatusAsync` returns a `ProjectionStatus` with the stored checkpoint, the last sequence number in the event
store and `Lag`, the difference, for a health check or a dashboard. Zero means caught up.

### Rebuilding

`PurgeAsync` on the store clears the views and resets the checkpoint in one transaction; a runner, running or
started afterwards, then rebuilds the projection from the first event. Readers see an empty read model while that
happens. To rebuild without that, give the new version of the projection a new name (`cars-v2`), run it alongside
until it has caught up, switch readers to it and purge the old one.

## Your own tables

Subclass `SqlServerProjection`. Handlers receive the `SqlTransaction`; implement `PurgeAsync(SqlTransaction, ...)` to
clear your tables for a rebuild. Creating and migrating the tables is yours.

```csharp
using dddlib.Persistence.Projections.SqlServer;
using Microsoft.Data.SqlClient;

public sealed class CarTableProjection : SqlServerProjection
{
    public CarTableProjection(string connectionString)
        : base(connectionString, "car-table")
    {
        this.When<CarRegistered>(async (e, transaction, cancellationToken) =>
        {
            await using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO [dbo].[Cars] ([Registration]) VALUES (@Registration);";
            command.Parameters.AddWithValue("@Registration", e.Registration);
            await command.ExecuteNonQueryAsync(cancellationToken);
        });
    }

    protected override async Task PurgeAsync(SqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM [dbo].[Cars];";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

await using var runner = new ProjectionRunner(new SqlServerEventStore(connectionString), new CarTableProjection(connectionString));
```

The projection is its own store: the checkpoint is moved first in the transaction and your writes follow, so a
failed page rolls your tables back too.

## A read model in its own database

The feed and the store take separate connection strings, so the read model may live in another database (or
server) from the event store. Both databases need the dddlib schema at version 2 or later: the event store's for the
`ReadEvents` procedure, the read model's for the checkpoint and views. Call `EnsureAsync` for each. The read model's
copy of the schema also holds the event store tables, unused.

## SQL Server schema

The `Projections` and `ProjectionViews` tables, the `ReadEvents` feed and the projection procedures are version 2 of
the one versioned dddlib schema. Install or upgrade it with `SqlServerProjectionsSchema.EnsureAsync(connectionString,
schema)`, which does the same as `SqlServerSchema.EnsureAsync`; see [SQL Server](sql-server.md) for the scripts,
running them yourself, the version check and rolling upgrades.

A page is committed in one transaction that moves the checkpoint first, under a row lock on the projection, and then
writes the views; a second runner for the same projection waits on that lock and then fails its checkpoint check
with error 50409, surfaced as a `ConcurrencyException` the runner treats as a lost race.

The feed reads committed events in sequence order. Commits are serialized on a store-wide lock, so the commit in
flight always holds the highest sequence numbers: a page that reaches it waits for it under `READ COMMITTED`, or
stops before it under read committed snapshot isolation, and either way the feed never returns an event below a
checkpoint later. Gaps left by rolled-back commits are skipped.

## Extending

`Sdk.IProjectionStore` is what the runner drives: read the checkpoint, apply a page atomically with the checkpoint
that follows it, purge. Implement it to project into another store, and `Sdk.ProjectionBase<TContext>` to give its
handlers whatever they write with. `Sdk.BufferedRepository<TIdentity, TEntity>` is the batch-scoped repository the
key/value stores hand to handlers. `IEventFeed` in dddlib.Persistence is the feed; implement it to project from
another event store.
