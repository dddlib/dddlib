# Event dispatcher

The **dddlib.Persistence.EventDispatcher** package delivers the events committed to an event store to your code, in
sequence order, with at-least-once delivery. It is how integrations and notifications react to what the domain has
done without being called from inside the aggregate root. For read models, use a [projection](projections.md)
instead: it commits its checkpoint with the read model, so each event takes effect exactly once.

```shell
dotnet add package dddlib.Persistence.EventDispatcher
```

The SQL Server host is in **dddlib.Persistence.EventDispatcher.SqlServer**:

```shell
dotnet add package dddlib.Persistence.EventDispatcher.SqlServer
```

## Model

Every committed event has a store-wide **sequence number**. A dispatcher is identified by a `Guid` and keeps its own
position in that sequence, so several independent consumers (say, a read model and an audit log) can each dispatch
every event once by using different dispatcher ids. Two hosts sharing a dispatcher id share the work instead.

Aggregate roots saved through the [memento repositories](memento-persistence.md) have their events appended to the
same store when the memento is saved, so they are dispatched the same way.

Events are handed out in numbered **batches** of consecutive events. The host delivers a batch one event at a time
and marks it dispatched once, after the last event it delivered: one round trip to the store per batch rather than
per event. If your code throws, the batch is abandoned: the events before the failure are marked, and the rest of the
batch is handed out again after the batch timeout, so order is preserved and nothing is skipped. Stopping the host
also marks the events it has delivered. That is why delivery is at least once: if the process dies before the mark,
the events of that batch already delivered (up to `BatchSize - 1` of them) are delivered again. Handlers must be
idempotent.

## Receiving events

Implement `IEventDispatcher` or wrap a delegate in `CustomEventDispatcher`:

```csharp
using dddlib.Persistence.EventDispatcher;

public sealed class CarNotifications : IEventDispatcher
{
    public Task DispatchAsync(long sequenceNumber, IReadOnlyDictionary<string, string> metadata, object @event, CancellationToken cancellationToken)
    {
        if (@event is CarRegistered registered)
        {
            // notify another system; it may be told twice, so make it idempotent
        }

        return Task.CompletedTask;
    }
}
```

`metadata` is what the event was stored with. On SQL Server that is the `hostname` and `timestamp` of the save, and
any entries a [custom repository](memento-persistence.md#custom-storage) appended with the events, such as a tenant
that the events themselves do not carry, or a message ID of that event's own. The in-memory store keeps the same:
`hostname`, `timestamp` and the entries of a `MemoryRepository<T>` that overrides `EventMetadata` or
`GetEventMetadata`. An entry stored as anything other than a string is left out. [Projections](projections.md) read
the same metadata from the feed. `CustomEventDispatcher` takes a delegate with or without the metadata.

### Receiving a batch at a time

To deliver a whole batch in one round trip, such as one publish to a bus, implement `IEventBatchDispatcher` instead.
It is an `IEventDispatcher`, so it goes wherever one does, and the host hands it each batch whole:

```csharp
using dddlib.Persistence.EventDispatcher;
using dddlib.Persistence.EventDispatcher.Sdk;

public sealed class CarEventPublisher(IBus bus) : IEventBatchDispatcher
{
    public Task DispatchAsync(IReadOnlyList<DispatchedEvent> events, CancellationToken cancellationToken) =>
        bus.PublishAsync(events.Select(e => e.Event), cancellationToken);
}
```

The batch is all or nothing: when `DispatchAsync` throws, none of it is marked and the whole batch is delivered again
after the batch timeout, and `DispatchFailed` reports the first event of the batch. `BatchSize` is then also the most
events one call receives.

## Hosting

The SQL Server host polls the same database the repository writes to. Install the schema first (see below).

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

await using var dispatcher = new SqlServerEventDispatcher(connectionString, new CarNotifications(), options);
await dispatcher.RunAsync(stoppingToken); // until the token is cancelled
```

`RunAsync` is the shape a `BackgroundService` wants:

```csharp
public sealed class CarNotificationService(SqlServerEventDispatcher dispatcher) : BackgroundService
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

When a handler throws, the host raises `DispatchFailed` with the sequence number, the event and the exception, marks
the events before it, abandons the batch and waits `BatchTimeout` before polling again so the abandoned batch comes back in order. The host
itself does not log; subscribe to `DispatchFailed` to do so. Cancelling the token stops the host without raising it.

## SQL Server schema

The dispatcher's `Batches` and `DispatchedEvents` tables and its `GetNextBatch` and `MarkDispatched` procedures are
part of the one versioned dddlib schema, together with the event store they read. Install or upgrade it with
`SqlServerEventDispatcherSchema.EnsureAsync(connectionString, schema)`, which does the same as
`SqlServerSchema.EnsureAsync` in dddlib.Persistence.SqlServer; see [SQL Server](sql-server.md) for the scripts, running
them yourself, the schema version check and rolling upgrades.

Polling takes an application lock on the dispatcher id, so two hosts with the same dispatcher id never get the same
batch. A host that cannot get the lock treats the poll as empty.

The event store in this version serializes commits on a store-wide application lock, so sequence numbers are assigned in
commit order and a dispatcher that has passed sequence number N never sees an event below N appear later.

## Extending

`Sdk.EventDispatcher` is the polling host over an `Sdk.IEventBatchStore`, which is the batch model above as an
interface. Implement it to dispatch from another store.
