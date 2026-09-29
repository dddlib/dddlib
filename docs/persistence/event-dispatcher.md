# Event dispatcher

The **dddlib.Persistence.EventDispatcher** package delivers the events committed to an event store to your code, in
sequence order, with at-least-once delivery. It is how projections, integrations and notifications react to what the
domain has done without being called from inside the aggregate root.

```shell
dotnet add package dddlib.Persistence.EventDispatcher
```

## Model

Every committed event has a store-wide **sequence number**. A dispatcher is identified by a `Guid` and keeps its own
position in that sequence, so several independent consumers (say, a read model and an audit log) can each dispatch
every event once by using different dispatcher ids. Two hosts sharing a dispatcher id share the work instead.

Aggregate roots saved through the [memento repositories](memento-persistence.md) have their events appended to the
same store when the memento is saved, so they are dispatched the same way.

Events are handed out in numbered **batches** of consecutive events. The host delivers a batch one event at a time,
marking each event dispatched as it goes. If your code throws, the batch is abandoned: the events already marked stay
marked, and the rest of the batch is handed out again after the batch timeout, so order is preserved and nothing is
skipped. That is also why delivery is at least once: an event whose handler completed but whose mark was lost is
delivered again. Handlers must be idempotent.

## Receiving events

Implement `IEventDispatcher` or wrap a delegate in `CustomEventDispatcher`:

```csharp
using dddlib.Persistence.EventDispatcher;

public sealed class CarProjection : IEventDispatcher
{
    public Task DispatchAsync(long sequenceNumber, object @event, CancellationToken cancellationToken)
    {
        if (@event is CarRegistered registered)
        {
            // update the read model
        }

        return Task.CompletedTask;
    }
}
```

## Hosting

The SQL Server host polls the same database the repository writes to. Run the package's schema script first (see
below).

```csharp
using dddlib.Persistence.EventDispatcher;
using dddlib.Persistence.EventDispatcher.SqlServer;

var options = new EventDispatcherOptions
{
    DispatcherId = new Guid("9f7c2b5e-2d4a-4b1e-9a8c-0d5f3f9c1a11"), // fixed per consumer
    BatchSize = 50,
    PollingInterval = TimeSpan.FromSeconds(1),
    MaxPollingInterval = TimeSpan.FromSeconds(10),
    BatchTimeout = TimeSpan.FromSeconds(30),
};

await using var dispatcher = new SqlServerEventDispatcher(connectionString, new CarProjection(), options);
await dispatcher.RunAsync(stoppingToken); // until the token is cancelled
```

`RunAsync` is the shape a `BackgroundService` wants:

```csharp
public sealed class CarProjectionService(SqlServerEventDispatcher dispatcher) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => dispatcher.RunAsync(stoppingToken);
}
```

Outside a host, `Start()` runs the loop on a background task and `StopAsync()` (or `DisposeAsync`) stops it after
the event in progress.

The in-memory host, `MemoryEventDispatcher`, takes the `MemoryEventStore` instance the repository was composed with
and is meant for tests.

## Polling

There is no push notification: `SqlDependency`, which v1 used, is not supported on Azure SQL Database. The host
polls. After an empty poll it waits `PollingInterval`, doubling the wait on each consecutive empty poll up to
`MaxPollingInterval`, and resets to `PollingInterval` as soon as a batch is found. A busy store is therefore drained
back to back and an idle one is polled a few times a minute.

## Failures

When a handler throws, the host raises `DispatchFailed` with the sequence number, the event and the exception,
abandons the batch and waits `BatchTimeout` before polling again so the abandoned batch comes back in order. The host
itself does not log; subscribe to `DispatchFailed` to do so. Cancelling the token stops the host without raising it.

## SQL Server schema

The package ships `06-SqlServerEventDispatcher.sql` under `content/Scripts`. Run it after the
[dddlib.Persistence scripts](sql-server.md); it needs `Events` from script 03. It creates the `Batches` and
`DispatchedEvents` tables and the `GetNextBatch` and `MarkDispatched` procedures. It is idempotent and targets `dbo`;
`SqlServerEventDispatcherScripts.Read(schema)` returns the text rewritten for another schema.

Polling takes an application lock on the dispatcher id, so two hosts with the same dispatcher id never get the same
batch. A host that cannot get the lock treats the poll as empty.

Script 03 in this version serializes commits on a store-wide application lock, so sequence numbers are assigned in
commit order and a dispatcher that has passed sequence number N never sees an event below N appear later.

## Extending

`Sdk.EventDispatcher` is the polling host over an `Sdk.IEventBatchStore`, which is the batch model above as an
interface. Implement it to dispatch from another store.
