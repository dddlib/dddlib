using dddlib.Persistence.EventDispatcher.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.EventDispatcher.Tests.Integration;

public class SqlServerEventDispatcherSchemaTests : SqlServerIntegration
{
    [Test]
    public async Task InstallingTheDispatcherInstallsTheEventStore()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));

        await SqlServerEventDispatcherSchema.EnsureAsync(this.ConnectionString, schema);

        foreach (var table in new[] { "Types", "Streams", "Events", "Batches", "DispatchedEvents", "Versions" })
        {
            await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT OBJECT_ID(N'[{schema}].[{table}]', N'U');")).IsNotNull();
        }

        await Assert.That((string?)await this.Database.ExecuteScalarAsync($"SELECT [Description] FROM [{schema}].[Versions] WHERE [Version] = 1;"))
            .StartsWith("dddlib.Persistence.EventDispatcher.SqlServer ");
    }

    [Test]
    public async Task BothPackagesProduceTheSameScript()
    {
        await Assert.That(SqlServerEventDispatcherSchema.GetScript("alternate")).IsEqualTo(global::dddlib.Persistence.SqlServer.SqlServerSchema.GetScript("alternate"));
    }
}
