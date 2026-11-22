using dddlib.Persistence.EventDispatcher.SqlServer;
using dddlib.Persistence.SqlServer;
using dddlib.Sdk;
using dddlib.Tests.Support;

namespace dddlib.Persistence.EventDispatcher.Tests.Integration;

public class SqlServerEventDispatcherSchemaTests : SqlServerIntegration
{
    [Test]
    public async Task InstallingTheDispatcherInstallsTheEventStore()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));

        var version = await SqlServerEventDispatcherSchema.EnsureAsync(this.ConnectionString, schema);

        foreach (var table in new[] { "Types", "Streams", "Events", "Batches", "DispatchedEvents", "Versions" })
        {
            await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT OBJECT_ID(N'[{schema}].[{table}]', N'U');")).IsNotNull();
        }

        await Assert.That((string?)await this.Database.ExecuteScalarAsync($"SELECT [Description] FROM [{schema}].[Versions] WHERE [Version] = 1;"))
            .StartsWith("dddlib.Persistence.EventDispatcher.SqlServer ");
        await Assert.That(version).IsEqualTo(new SqlServerEventDispatcherSchemaVersion(schema, version.RequiredVersion, version.RequiredVersion, 1));
        await Assert.That(version.IsAhead).IsFalse();
    }

    [Test]
    public async Task ReportsWhenTheSchemaIsAheadOfThePackage()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));
        var installed = await SqlServerEventDispatcherSchema.EnsureAsync(this.ConnectionString, schema);
        await this.Database.ExecuteScriptAsync($"INSERT INTO [{schema}].[Versions] ([Version]) VALUES (99);");

        var version = await SqlServerEventDispatcherSchema.EnsureAsync(this.ConnectionString, schema);
        var batch = await new SqlServerEventBatchStore(this.ConnectionString, schema).GetNextBatchAsync(Guid.NewGuid(), 10, TimeSpan.FromSeconds(30));

        await Assert.That(version).IsEqualTo(new SqlServerEventDispatcherSchemaVersion(schema, 99, installed.RequiredVersion, 1));
        await Assert.That(version.IsAhead).IsTrue();
        await Assert.That(batch).IsNull();
    }

    [Test]
    public async Task GetVersionReadsWithoutChangingAnything()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));

        var missing = await SqlServerEventDispatcherSchema.GetVersionAsync(this.ConnectionString, schema);
        var schemaId = await this.Database.ExecuteScalarAsync($"SELECT SCHEMA_ID(N'{schema}');");
        var installed = await SqlServerEventDispatcherSchema.EnsureAsync(this.ConnectionString, schema);
        var current = await SqlServerEventDispatcherSchema.GetVersionAsync(this.ConnectionString, schema);

        await Assert.That(missing).IsEqualTo(new SqlServerEventDispatcherSchemaVersion(schema, 0, installed.RequiredVersion, 1));
        await Assert.That(missing.IsCompatible).IsFalse();
        await Assert.That(schemaId).IsNull();
        await Assert.That(current).IsEqualTo(installed);
        await Assert.That(current.IsCompatible).IsTrue();
    }

    [Test]
    public async Task FailsLoudlyWhenThePackageIsTooOldForTheSchema()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));
        await SqlServerEventDispatcherSchema.EnsureAsync(this.ConnectionString, schema);
        await this.Database.ExecuteScriptAsync($"INSERT INTO [{schema}].[Versions] ([Version], [MinimumRequiredVersion]) VALUES (99, 98);");

        await Assert.That(() => new SqlServerEventBatchStore(this.ConnectionString, schema).GetNextBatchAsync(Guid.NewGuid(), 10, TimeSpan.FromSeconds(30)))
            .Throws<PersistenceException>()
            .WithMessageContaining($"The SQL Server schema [{schema}] is at version 99, which supports packages that require version 98 or later, but dddlib.Persistence.EventDispatcher.SqlServer ");
        await Assert.That(() => (Task)SqlServerEventDispatcherSchema.EnsureAsync(this.ConnectionString, schema))
            .Throws<PersistenceException>()
            .WithMessageContaining("To fix this issue");
    }

    [Test]
    public async Task FailsLoudlyWhenTheSchemaIsBehind()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));
        await this.Database.ExecuteScriptAsync($"CREATE SCHEMA [{schema}];");
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString, schema);

        await Assert.That(() => batchStore.GetNextBatchAsync(Guid.NewGuid(), 10, TimeSpan.FromSeconds(30)))
            .Throws<PersistenceException>()
            .WithMessageContaining($"The SQL Server schema [{schema}] is at version 0, but dddlib.Persistence.EventDispatcher.SqlServer ");

        await SqlServerEventDispatcherSchema.EnsureAsync(this.ConnectionString, schema);

        await Assert.That(await batchStore.GetNextBatchAsync(Guid.NewGuid(), 10, TimeSpan.FromSeconds(30))).IsNull();
    }

    private sealed class Event
    {
    }

    // Version 4 reads the events' metadata for the dispatcher. Events stored before it have dddlib's entries only, or
    // none (https://github.com/dddlib/dddlib/issues/52).
    [Test]
    public async Task UpgradesFromVersion3AndDispatchesTheEventsStoredBefore()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));
        await SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, SqlServerSchemaInstaller.Scripts.Take(3).ToArray(), CancellationToken.None);
        await this.Database.ExecuteScriptAsync($$"""
            INSERT INTO [{{schema}}].[Types] ([Name]) VALUES ('{{typeof(Event).GetSerializedName()}}');
            INSERT INTO [{{schema}}].[Streams] ([Id], [Revision], [State]) VALUES (NEWID(), 2, 'state');
            INSERT INTO [{{schema}}].[Events] ([StreamLinkId], [StreamRevision], [TypeId], [Metadata], [Payload], [CorrelationId], [SequenceNumber])
            SELECT [Stream].[LinkId], [Event].[StreamRevision], [Type].[Id], [Event].[Metadata], N'{}', NEWID(), [Event].[StreamRevision]
            FROM [{{schema}}].[Streams] [Stream]
                CROSS JOIN [{{schema}}].[Types] [Type]
                CROSS JOIN (VALUES (1, N'{"hostname":"web-01","timestamp":"2026-10-01T00:00:00Z"}'), (2, NULL)) AS [Event] ([StreamRevision], [Metadata]);
            """);

        var upgraded = await SqlServerEventDispatcherSchema.EnsureAsync(this.ConnectionString, schema);
        var batch = await new SqlServerEventBatchStore(this.ConnectionString, schema).GetNextBatchAsync(Guid.NewGuid(), 10, TimeSpan.FromSeconds(30));

        await Assert.That(upgraded).IsEqualTo(new SqlServerEventDispatcherSchemaVersion(schema, SqlServerSchemaInstaller.RequiredVersion, SqlServerSchemaInstaller.RequiredVersion, 1));
        await Assert.That(batch!.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([1L, 2L]);
        await Assert.That(batch.Events[0].Metadata).IsEquivalentTo(new Dictionary<string, string> { ["hostname"] = "web-01", ["timestamp"] = "2026-10-01T00:00:00Z" });
        await Assert.That(batch.Events[1].Metadata).IsEmpty();
    }

    // Before version 10 a batch that timed out was marked complete, and the dispatcher read again from its position. The
    // upgrade keeps that: such a batch is handed out again, as is the rest of an open batch partly marked before the
    // upgrade, and a batch completed by its mark stays done (https://github.com/dddlib/dddlib/issues/53).
    [Test]
    public async Task UpgradesFromVersion9AndKeepsTheBatchesHandedOutBefore()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));
        var timedOut = Guid.NewGuid();
        var partlyMarked = Guid.NewGuid();
        var completed = Guid.NewGuid();
        await SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, SqlServerSchemaInstaller.Scripts.Take(9).ToArray(), CancellationToken.None);
        await this.Database.ExecuteScriptAsync($$"""
            INSERT INTO [{{schema}}].[Types] ([Name]) VALUES ('{{typeof(Event).GetSerializedName()}}');
            INSERT INTO [{{schema}}].[Streams] ([Id], [Revision], [State]) VALUES (NEWID(), 4, 'state');
            INSERT INTO [{{schema}}].[Events] ([StreamLinkId], [StreamRevision], [TypeId], [Payload], [CorrelationId], [SequenceNumber])
            SELECT [Stream].[LinkId], [Event].[StreamRevision], [Type].[Id], N'{}', NEWID(), [Event].[StreamRevision]
            FROM [{{schema}}].[Streams] [Stream]
                CROSS JOIN [{{schema}}].[Types] [Type]
                CROSS JOIN (VALUES (1), (2), (3), (4)) AS [Event] ([StreamRevision]);
            INSERT INTO [{{schema}}].[DispatchedEvents] ([DispatcherId], [SequenceNumber])
            VALUES ('{{timedOut}}', 1), ('{{partlyMarked}}', 2), ('{{completed}}', 3);
            INSERT INTO [{{schema}}].[Batches] ([DispatcherId], [SequenceNumber], [LastSequenceNumber], [Timestamp], [Complete])
            VALUES
                ('{{timedOut}}', 1, 1, DATEADD(MINUTE, -10, SYSUTCDATETIME()), 1),
                ('{{timedOut}}', 2, 3, DATEADD(MINUTE, -5, SYSUTCDATETIME()), 1),
                ('{{partlyMarked}}', 1, 3, DATEADD(MINUTE, -5, SYSUTCDATETIME()), 0),
                ('{{completed}}', 1, 3, DATEADD(MINUTE, -5, SYSUTCDATETIME()), 1);
            """);

        await SqlServerEventDispatcherSchema.EnsureAsync(this.ConnectionString, schema);
        var batchStore = new SqlServerEventBatchStore(this.ConnectionString, schema);

        await Assert.That((await batchStore.GetNextBatchAsync(timedOut, 10, TimeSpan.FromSeconds(30)))!.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([2L, 3L, 4L]);
        await Assert.That((await batchStore.GetNextBatchAsync(partlyMarked, 10, TimeSpan.FromSeconds(30)))!.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([3L]);
        await Assert.That((await batchStore.GetNextBatchAsync(completed, 10, TimeSpan.FromSeconds(30)))!.Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([4L]);
    }

    [Test]
    public async Task BothPackagesProduceTheSameScript()
    {
        await Assert.That(SqlServerEventDispatcherSchema.GetScript("alternate")).IsEqualTo(global::dddlib.Persistence.SqlServer.SqlServerSchema.GetScript("alternate"));
    }
}
