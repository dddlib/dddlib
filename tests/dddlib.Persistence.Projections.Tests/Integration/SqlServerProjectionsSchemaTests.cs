using dddlib.Persistence.Projections.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Projections.Tests.Integration;

public class SqlServerProjectionsSchemaTests : SqlServerIntegration
{
    [Test]
    public async Task InstallingProjectionsInstallsTheEventStore()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));

        var version = await SqlServerProjectionsSchema.EnsureAsync(this.ConnectionString, schema);

        foreach (var table in new[] { "Types", "Streams", "Events", "Projections", "ProjectionViews", "Versions" })
        {
            await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT OBJECT_ID(N'[{schema}].[{table}]', N'U');")).IsNotNull();
        }

        await Assert.That((string?)await this.Database.ExecuteScalarAsync($"SELECT [Description] FROM [{schema}].[Versions] WHERE [Version] = 2;"))
            .StartsWith("dddlib.Persistence.Projections.SqlServer ");
        await Assert.That(version).IsEqualTo(new SqlServerProjectionsSchemaVersion(schema, version.RequiredVersion, version.RequiredVersion, 1));
        await Assert.That(version.RequiredVersion).IsGreaterThanOrEqualTo(2);
        await Assert.That(version.IsAhead).IsFalse();
    }

    [Test]
    public async Task ReportsWhenTheSchemaIsAheadOfThePackage()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));
        var installed = await SqlServerProjectionsSchema.EnsureAsync(this.ConnectionString, schema);
        await this.Database.ExecuteScriptAsync($"INSERT INTO [{schema}].[Versions] ([Version]) VALUES (99);");

        var version = await SqlServerProjectionsSchema.EnsureAsync(this.ConnectionString, schema);
        var view = await new SqlServerRepository<string, View>(this.ConnectionString, "ahead", schema).GetAsync("a");

        await Assert.That(version).IsEqualTo(new SqlServerProjectionsSchemaVersion(schema, 99, installed.RequiredVersion, 1));
        await Assert.That(version.IsAhead).IsTrue();
        await Assert.That(view).IsNull();
    }

    [Test]
    public async Task GetVersionReadsWithoutChangingAnything()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));

        var missing = await SqlServerProjectionsSchema.GetVersionAsync(this.ConnectionString, schema);
        var schemaId = await this.Database.ExecuteScalarAsync($"SELECT SCHEMA_ID(N'{schema}');");
        var installed = await SqlServerProjectionsSchema.EnsureAsync(this.ConnectionString, schema);
        var current = await SqlServerProjectionsSchema.GetVersionAsync(this.ConnectionString, schema);

        await Assert.That(missing).IsEqualTo(new SqlServerProjectionsSchemaVersion(schema, 0, installed.RequiredVersion, 1));
        await Assert.That(missing.IsCompatible).IsFalse();
        await Assert.That(schemaId).IsNull();
        await Assert.That(current).IsEqualTo(installed);
        await Assert.That(current.IsCompatible).IsTrue();
    }

    [Test]
    public async Task FailsLoudlyWhenTheSchemaIsBehind()
    {
        var schema = string.Concat("s", Guid.NewGuid().ToString("N"));
        await this.Database.ExecuteScriptAsync($"CREATE SCHEMA [{schema}];");
        var repository = new SqlServerRepository<string, View>(this.ConnectionString, "behind", schema);

        await Assert.That(() => repository.GetAsync("a"))
            .Throws<PersistenceException>()
            .WithMessageContaining($"The SQL Server schema [{schema}] is at version 0, but dddlib.Persistence.Projections.SqlServer ");
        await Assert.That(() => repository.GetAsync("a"))
            .Throws<PersistenceException>()
            .WithMessageContaining("SqlServerProjectionsSchema.EnsureAsync (dddlib.Persistence.Projections.SqlServer)");

        await SqlServerProjectionsSchema.EnsureAsync(this.ConnectionString, schema);

        await Assert.That(await repository.GetAsync("a")).IsNull();
    }

    [Test]
    public async Task AllPackagesProduceTheSameScript()
    {
        var script = SqlServerProjectionsSchema.GetScript("alternate");

        await Assert.That(script).IsEqualTo(global::dddlib.Persistence.SqlServer.SqlServerSchema.GetScript("alternate"));
        await Assert.That(script).IsEqualTo(global::dddlib.Persistence.EventDispatcher.SqlServer.SqlServerEventDispatcherSchema.GetScript("alternate"));
    }

    private sealed record View(string Name);
}
