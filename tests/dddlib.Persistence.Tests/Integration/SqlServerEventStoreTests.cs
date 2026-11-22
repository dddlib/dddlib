using System.Data;
using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
using dddlib.Sdk;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;
using TUnit.Assertions.Enums;

namespace dddlib.Persistence.Tests.Integration;

// Serial: the feed tests read the store-wide sequence, which every commit in the class advances.
[NotInParallel]
public class SqlServerEventStoreTests : SqlServerIntegration
{
    [Test]
    public async Task TrySaveSingleEvent()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var streamId = Guid.NewGuid();
        var events = new[] { new Event { Id = 1, Value = "One" } };

        var commitState = await eventStore.CommitStreamAsync(streamId, events, Guid.NewGuid(), null);
        var stream = await eventStore.GetStreamAsync(streamId, 0);

        await Assert.That(stream.Events).Count().IsEqualTo(1);
        await Assert.That(stream.Events[0]).IsTypeOf<Event>();
        await Assert.That(((Event)stream.Events[0]).Id).IsEqualTo(1);
        await Assert.That(((Event)stream.Events[0]).Value).IsEqualTo("One");
        await Assert.That(stream.State).IsEqualTo(commitState);
    }

    [Test]
    public async Task TrySaveMultipleEvents()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var streamId = Guid.NewGuid();
        var events = new[]
        {
            new Event { Id = 1, Value = "One" },
            new Event { Id = 2, Value = "Two" },
        };

        var commitState = await eventStore.CommitStreamAsync(streamId, events, Guid.NewGuid(), null);
        var stream = await eventStore.GetStreamAsync(streamId, 0);

        await Assert.That(stream.Events).Count().IsEqualTo(2);
        await Assert.That(((Event)stream.Events[0]).Id).IsEqualTo(1);
        await Assert.That(((Event)stream.Events[0]).Value).IsEqualTo("One");
        await Assert.That(((Event)stream.Events[1]).Id).IsEqualTo(2);
        await Assert.That(((Event)stream.Events[1]).Value).IsEqualTo("Two");
        await Assert.That(stream.State).IsEqualTo(commitState);
    }

    [Test]
    public async Task TrySaveEventsInMultipleCommits()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var streamId = Guid.NewGuid();
        var events1 = new[] { new Event { Id = 1, Value = "One" } };
        var events2 = new[] { new Event { Id = 2, Value = "Two" } };

        var firstCommitState = await eventStore.CommitStreamAsync(streamId, events1, Guid.NewGuid(), null);
        var secondCommitState = await eventStore.CommitStreamAsync(streamId, events2, Guid.NewGuid(), firstCommitState);
        var stream = await eventStore.GetStreamAsync(streamId, 0);

        await Assert.That(stream.Events).Count().IsEqualTo(2);
        await Assert.That(((Event)stream.Events[0]).Id).IsEqualTo(1);
        await Assert.That(((Event)stream.Events[1]).Id).IsEqualTo(2);
        await Assert.That(stream.State).IsEqualTo(secondCommitState);
        await Assert.That(firstCommitState).IsNotEqualTo(secondCommitState);
    }

    [Test]
    public async Task TrySaveEventsForMultipleStreams()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var stream1Id = Guid.NewGuid();
        var stream2Id = Guid.NewGuid();
        var events1 = new[] { new Event { Id = 1, Value = "One" } };
        var events2 = new[] { new Event { Id = 2, Value = "Two" } };

        var stream1CommitState = await eventStore.CommitStreamAsync(stream1Id, events1, Guid.NewGuid(), null);
        var stream2CommitState = await eventStore.CommitStreamAsync(stream2Id, events2, Guid.NewGuid(), null);
        var stream1 = await eventStore.GetStreamAsync(stream1Id, 0);
        var stream2 = await eventStore.GetStreamAsync(stream2Id, 0);

        await Assert.That(stream1.Events).Count().IsEqualTo(1);
        await Assert.That(((Event)stream1.Events[0]).Id).IsEqualTo(1);
        await Assert.That(stream1.State).IsEqualTo(stream1CommitState);
        await Assert.That(stream2.Events).Count().IsEqualTo(1);
        await Assert.That(((Event)stream2.Events[0]).Id).IsEqualTo(2);
        await Assert.That(stream2.State).IsEqualTo(stream2CommitState);
    }

    [Test]
    public async Task ExpectClientSideConcurrencyException()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var streamId = Guid.NewGuid();
        var events1 = new[] { new Event { Id = 1, Value = "One" } };
        var events2 = new[] { new Event { Id = 1, Value = "AnotherOne" } };

        await eventStore.CommitStreamAsync(streamId, events1, Guid.NewGuid(), null);
        Func<Task> action = () => eventStore.CommitStreamAsync(streamId, events2, Guid.NewGuid(), null);

        await Assert.That(action).Throws<ConcurrencyException>();
    }

    [Test]
    public async Task ExpectServerSideConcurrencyException()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var streamId = Guid.NewGuid();
        var events = new[] { new Event { Id = 1, Value = "One" } };

        // Hold the stream's commit lock on another connection so the commit cannot acquire it in time.
        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "EXEC sp_getapplock @Resource = @StreamId, @LockMode = 'Exclusive', @LockTimeout = 1000;";
        command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;
        await command.ExecuteNonQueryAsync();

        Func<Task> action = () => eventStore.CommitStreamAsync(streamId, events, Guid.NewGuid(), null);

        await Assert.That(action).Throws<ConcurrencyException>();
    }

    [Test]
    public async Task ReadsEventsInSequenceOrder()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var stream1Id = Guid.NewGuid();
        var stream2Id = Guid.NewGuid();
        var correlation1 = Guid.NewGuid();
        var correlation2 = Guid.NewGuid();
        var first = await eventStore.GetLastSequenceNumberAsync();
        await eventStore.CommitStreamAsync(stream1Id, [new Event { Id = 1, Value = "One" }, new Event { Id = 2, Value = "Two" }], correlation1, null);
        await eventStore.CommitStreamAsync(stream2Id, [new Event { Id = 3, Value = "Three" }], correlation2, null);

        var page = await eventStore.ReadEventsAsync(first, 10);

        await Assert.That(page.EndSequenceNumber).IsEqualTo(first + 3);
        await Assert.That(page.Events.Select(static e => e.SequenceNumber)).IsInOrder();
        await Assert.That(page.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1, 2, 3]);
        await Assert.That(page.Events.Select(static e => e.StreamId)).IsEquivalentTo([stream1Id, stream1Id, stream2Id]);
        await Assert.That(page.Events.Select(static e => e.StreamRevision)).IsEquivalentTo([1, 2, 1]);
        await Assert.That(page.Events.Select(static e => e.CorrelationId)).IsEquivalentTo([correlation1, correlation1, correlation2]);
    }

    [Test]
    public async Task ReadsOnlyAfterTheSequenceNumber()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var first = await eventStore.GetLastSequenceNumberAsync();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }, new Event { Id = 3 }], Guid.NewGuid(), null);

        var second = await eventStore.ReadEventsAsync(first + 1, 1);
        var rest = await eventStore.ReadEventsAsync(first + 2, 10);
        var none = await eventStore.ReadEventsAsync(first + 3, 10);

        await Assert.That(second.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([2]);
        await Assert.That(second.EndSequenceNumber).IsEqualTo(first + 2);
        await Assert.That(rest.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([3]);
        await Assert.That(rest.EndSequenceNumber).IsEqualTo(first + 3);
        await Assert.That(none.Events).IsEmpty();
        await Assert.That(none.EndSequenceNumber).IsEqualTo(first + 3);
    }

    // The page spans every event so the reader's checkpoint moves past the ones it did not ask for.
    [Test]
    public async Task ReadsOnlyTheRequestedEventTypes()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var first = await eventStore.GetLastSequenceNumberAsync();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new OtherEvent(), new Event { Id = 3 }], Guid.NewGuid(), null);

        var others = await eventStore.ReadEventsAsync(first, 10, [typeof(OtherEvent)]);
        var firstPage = await eventStore.ReadEventsAsync(first, 2, [typeof(Event)]);
        var all = await eventStore.ReadEventsAsync(first, 10, []);

        await Assert.That(others.Events.Select(static e => e.Event)).Count().IsEqualTo(1);
        await Assert.That(others.Events[0].Event).IsTypeOf<OtherEvent>();
        await Assert.That(others.EndSequenceNumber).IsEqualTo(first + 3);
        await Assert.That(firstPage.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1]);
        await Assert.That(firstPage.EndSequenceNumber).IsEqualTo(first + 2);
        await Assert.That(all.Events).Count().IsEqualTo(3);
    }

    [Test]
    public async Task ReportsTheLastSequenceNumber()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);

        var before = await eventStore.GetLastSequenceNumberAsync();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }, new Event { Id = 2 }], Guid.NewGuid(), null);
        var after = await eventStore.GetLastSequenceNumberAsync();

        await Assert.That(after).IsEqualTo(before + 2);
    }

    // A commit that rolls back has already taken its sequence numbers. The feed is bounded by actual events, so the
    // gap is skipped and the page ends on an event.
    [Test]
    public async Task SkipsGapsLeftByRolledBackCommits()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var first = await eventStore.GetLastSequenceNumberAsync();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }], Guid.NewGuid(), null);
        await this.Database.ExecuteScalarAsync("SELECT NEXT VALUE FOR [dbo].[SequenceNumber];");
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 3 }], Guid.NewGuid(), null);

        var page = await eventStore.ReadEventsAsync(first, 10);
        var afterTheGap = await eventStore.ReadEventsAsync(first + 1, 10);

        await Assert.That(page.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([first + 1, first + 3]);
        await Assert.That(page.EndSequenceNumber).IsEqualTo(first + 3);
        await Assert.That(afterTheGap.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([first + 3]);
    }

    // Commits are serialized, so the commit in flight holds the highest sequence numbers. A page that stops before it
    // returns at once; a page that reaches it waits for it, under READ COMMITTED, and then includes it. Either way the
    // feed never returns an event with a lower sequence number later than one with a higher one.
    [Test]
    public async Task WaitsForACommitInFlight()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var first = await eventStore.GetLastSequenceNumberAsync();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }], Guid.NewGuid(), null);

        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await CommitByHandAsync(transaction, typeof(Event).GetSerializedName(), """{"Id":2}""");

        var committedPrefix = await eventStore.ReadEventsAsync(first, 1);
        var reaching = eventStore.ReadEventsAsync(first, 10);
        var completedWhileInFlight = reaching.Wait(TimeSpan.FromSeconds(1));
        await transaction.CommitAsync();
        var page = await reaching;

        await Assert.That(committedPrefix.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1]);
        await Assert.That(completedWhileInFlight).IsFalse();
        await Assert.That(page.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1, 2]);
        await Assert.That(page.EndSequenceNumber).IsEqualTo(first + 2);
    }

    // Commits are serialized per schema: the store-wide lock is named after the schema's event table, so a commit in
    // flight in one schema holds up the commits of that schema only (https://github.com/dddlib/dddlib/issues/57).
    [Test]
    public async Task ACommitInFlightHoldsUpCommitsInItsOwnSchemaOnly()
    {
        var otherSchema = string.Concat("s", Guid.NewGuid().ToString("N"));
        await this.Database.CreateSchemaAsync(otherSchema);
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var otherEventStore = new SqlServerEventStore(this.ConnectionString, otherSchema);
        await otherEventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 0 }], Guid.NewGuid(), null);

        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await CommitByHandAsync(transaction, typeof(Event).GetSerializedName(), """{"Id":1}""");

        var sameSchema = eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 2 }], Guid.NewGuid(), null);
        var single = await otherEventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 3 }], Guid.NewGuid(), null);
        var multiple = await otherEventStore.CommitStreamsAsync(
            [
                new StreamCommit(Guid.NewGuid(), [new Event { Id = 4 }], null),
                new StreamCommit(Guid.NewGuid(), [new Event { Id = 5 }], null),
            ],
            Guid.NewGuid());

        await Assert.That(single).IsNotNull();
        await Assert.That(multiple).Count().IsEqualTo(2);
        await Assert.That(() => (Task)sameSchema).Throws<ConcurrencyException>().WithMessageContaining("event sequence lock");
        await transaction.RollbackAsync();
    }

    [Test]
    public async Task MissingEventTypeFailsLoudly()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var first = await eventStore.GetLastSequenceNumberAsync();
        await using (var connection = new SqlConnection(this.ConnectionString))
        {
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            await CommitByHandAsync(transaction, "Missing.Namespace.Retired, Missing.Assembly", "{}");
            await transaction.CommitAsync();
        }

        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 2 }], Guid.NewGuid(), null);

        await Assert.That(() => (Task)eventStore.ReadEventsAsync(first, 10))
            .Throws<PersistenceException>()
            .WithMessageContaining("Cannot deserialize into type of 'Missing.Namespace.Retired' as that type does not exist in the assembly 'Missing.Assembly'");

        var handled = await eventStore.ReadEventsAsync(first, 10, [typeof(Event)]);

        await Assert.That(handled.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([2]);
        await Assert.That(handled.EndSequenceNumber).IsEqualTo(first + 2);
    }

    // The feed carries what each event was stored with, read as the event dispatcher reads it: the string entries of
    // the stored JSON object, and nothing for metadata that is missing or not an object
    // (https://github.com/dddlib/dddlib/issues/60).
    [Test]
    public async Task FeedCarriesTheMetadataOfEachEvent()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var first = await eventStore.GetLastSequenceNumberAsync();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 1 }], Guid.NewGuid(), null);
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 2 }, new Event { Id = 3 }, new Event { Id = 4 }], Guid.NewGuid(), null);
        await this.Database.ExecuteScriptAsync($$$"""
            UPDATE [dbo].[Events] SET [Metadata] = NULL WHERE [SequenceNumber] = {{{first + 2}}};
            UPDATE [dbo].[Events] SET [Metadata] = N'[1,2]' WHERE [SequenceNumber] = {{{first + 3}}};
            UPDATE [dbo].[Events] SET [Metadata] = N'{"tenant":"acme","count":2,"nested":{"a":"b"}}' WHERE [SequenceNumber] = {{{first + 4}}};
            """);

        var page = await eventStore.ReadEventsAsync(first, 10);

        await Assert.That(page.Events[0].Metadata.Keys).IsEquivalentTo(["hostname", "timestamp"]);
        await Assert.That(page.Events[0].Metadata["hostname"]).IsEqualTo(Environment.MachineName);
        await Assert.That(page.Events[1].Metadata).IsEmpty();
        await Assert.That(page.Events[2].Metadata).IsEmpty();
        await Assert.That(page.Events[3].Metadata).IsEquivalentTo(new Dictionary<string, string> { ["tenant"] = "acme" });
    }

    private static async Task CommitByHandAsync(SqlTransaction transaction, string typeName, string payload, string schema = "dbo")
    {
        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            EXEC [{schema}].[TryAddType] @Name = @TypeName;
            DECLARE @Events [{schema}].[EventList];
            INSERT INTO @Events ([Index], [TypeId], [Payload])
            SELECT 1, [Id], @Payload FROM [{schema}].[Types] WHERE [Name] = @TypeName COLLATE SQL_Latin1_General_CP1_CS_AS;
            EXEC [{schema}].[CommitStream] @StreamId = @StreamId, @Events = @Events, @Metadata = NULL, @CorrelationId = @CorrelationId, @PreCommitState = NULL;
            """;
        command.Parameters.Add("@TypeName", SqlDbType.VarChar, 511).Value = typeName;
        command.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value = payload;
        command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        command.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        await command.ExecuteNonQueryAsync();
    }

    [Test]
    public async Task CommitStreamsAssignsContiguousSequenceNumbers()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var stream1Id = Guid.NewGuid();
        var stream2Id = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        await eventStore.CommitStreamAsync(Guid.NewGuid(), [new Event { Id = 0 }], Guid.NewGuid(), null);
        var first = await eventStore.GetLastSequenceNumberAsync();

        var states = await eventStore.CommitStreamsAsync(
            [
                new StreamCommit(stream1Id, [new Event { Id = 1 }, new Event { Id = 2 }], null),
                new StreamCommit(stream2Id, [new Event { Id = 3 }], null),
            ],
            correlationId);
        var page = await eventStore.ReadEventsAsync(first, 10);
        var stream1 = await eventStore.GetStreamAsync(stream1Id, 0);
        var stream2 = await eventStore.GetStreamAsync(stream2Id, 0);

        await Assert.That(states).Count().IsEqualTo(2);
        await Assert.That(stream1.State).IsEqualTo(states[0]);
        await Assert.That(stream2.State).IsEqualTo(states[1]);
        await Assert.That(page.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([first + 1, first + 2, first + 3], CollectionOrdering.Matching);
        await Assert.That(page.Events.Select(static e => ((Event)e.Event).Id)).IsEquivalentTo([1, 2, 3], CollectionOrdering.Matching);
        await Assert.That(page.Events.Select(static e => e.StreamRevision)).IsEquivalentTo([1, 2, 1], CollectionOrdering.Matching);
        await Assert.That(page.Events.Select(static e => e.CorrelationId).Distinct()).IsEquivalentTo([correlationId]);
    }

    [Test]
    public async Task CommitStreamsNamesTheStaleStream()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var currentId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        var current = await eventStore.CommitStreamAsync(currentId, [new Event { Id = 1 }], Guid.NewGuid(), null);
        var stale = await eventStore.CommitStreamAsync(staleId, [new Event { Id = 2 }], Guid.NewGuid(), null);
        await eventStore.CommitStreamAsync(staleId, [new Event { Id = 3 }], Guid.NewGuid(), stale);
        var last = await eventStore.GetLastSequenceNumberAsync();

        Func<Task> commit = () => eventStore.CommitStreamsAsync(
            [
                new StreamCommit(currentId, [new Event { Id = 4 }], current),
                new StreamCommit(staleId, [new Event { Id = 5 }], stale),
            ],
            Guid.NewGuid());
        Func<Task> create = () => eventStore.CommitStreamsAsync([new StreamCommit(currentId, [new Event { Id = 6 }], null)], Guid.NewGuid());

        var exception = await Assert.That(commit).Throws<StreamConcurrencyException>();
        var exists = await Assert.That(create).Throws<StreamConcurrencyException>();

        await Assert.That(exception!.StreamId).IsEqualTo(staleId);
        await Assert.That(exists!.StreamId).IsEqualTo(currentId);
        await Assert.That(await eventStore.GetLastSequenceNumberAsync()).IsEqualTo(last);
        await Assert.That((await eventStore.GetStreamAsync(currentId, 0)).State).IsEqualTo(current);
    }

    [Test]
    public async Task CommitStreamsRejectsTheSameStreamTwice()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var streamId = Guid.NewGuid();
        var last = await eventStore.GetLastSequenceNumberAsync();

        Func<Task> commit = () => eventStore.CommitStreamsAsync(
            [
                new StreamCommit(streamId, [new Event { Id = 1 }], null),
                new StreamCommit(streamId, [new Event { Id = 2 }], null),
            ],
            Guid.NewGuid());

        await Assert.That(commit).Throws<ArgumentException>();
        await Assert.That(await eventStore.GetLastSequenceNumberAsync()).IsEqualTo(last);
    }

    // A commit of several streams takes every stream's lock, in identity order, before the store-wide lock; a single
    // commit takes its stream's lock and then the store-wide lock. Both run here against the same streams, the batches
    // in opposite orders: any of them may lose a race (a ConcurrencyException), but none may be a deadlock victim.
    [Test]
    public async Task CommitStreamsAndSingleCommitsDoNotDeadlock()
    {
        var eventStore = new SqlServerEventStore(this.ConnectionString);
        var streamIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var streamId in streamIds)
        {
            await eventStore.CommitStreamAsync(streamId, [new Event { Id = 0 }], Guid.NewGuid(), null);
        }

        var batches = 0;

        async Task CommitManyAsync(IEnumerable<Guid> ids)
        {
            for (var round = 0; round < 20; round++)
            {
                var commits = new List<StreamCommit>();
                foreach (var id in ids)
                {
                    commits.Add(new StreamCommit(id, [new Event { Id = round }], (await eventStore.GetStreamAsync(id, 0)).State));
                }

                try
                {
                    await eventStore.CommitStreamsAsync(commits, Guid.NewGuid());
                    Interlocked.Increment(ref batches);
                }
                catch (ConcurrencyException)
                {
                }
            }
        }

        async Task CommitOneAsync(Guid id)
        {
            for (var round = 0; round < 20; round++)
            {
                try
                {
                    await eventStore.CommitStreamAsync(id, [new Event { Id = round }], Guid.NewGuid(), (await eventStore.GetStreamAsync(id, 0)).State);
                }
                catch (ConcurrencyException)
                {
                }
            }
        }

        await Task.WhenAll(
            CommitManyAsync(streamIds),
            CommitManyAsync(streamIds.Reverse()),
            CommitOneAsync(streamIds[0]),
            CommitOneAsync(streamIds[1]),
            CommitOneAsync(streamIds[2]));

        await Assert.That(batches).IsGreaterThan(0);
    }

    private sealed class Event
    {
        public int Id { get; set; }

        public string? Value { get; set; }
    }

    private sealed class OtherEvent
    {
        public string? Value { get; set; }
    }
}
