using dddlib.Persistence.EventDispatcher.SqlServer;
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

    [Test]
    public async Task BothPackagesProduceTheSameScript()
    {
        await Assert.That(SqlServerEventDispatcherSchema.GetScript("alternate")).IsEqualTo(global::dddlib.Persistence.SqlServer.SqlServerSchema.GetScript("alternate"));
    }
}
