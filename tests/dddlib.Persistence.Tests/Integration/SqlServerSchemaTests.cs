using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.Tests.Integration;

// Each test installs into a schema of its own, so it starts from nothing inside the per-class database.
public class SqlServerSchemaTests : SqlServerIntegration
{
    private static readonly string[] Tables = ["Types", "NaturalKeys", "Streams", "Events", "Snapshots", "Mementos", "Batches", "DispatchedEvents", "Versions"];

    [Test]
    public async Task CreatesTheSchemaWithEveryObject()
    {
        var schema = NewSchema();

        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        foreach (var table in Tables)
        {
            await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT OBJECT_ID(N'[{schema}].[{table}]', N'U');")).IsNotNull();
        }

        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(1);
        await Assert.That((string?)await this.Database.ExecuteScalarAsync($"SELECT [Description] FROM [{schema}].[Versions] WHERE [Version] = 1;"))
            .StartsWith("dddlib.Persistence.SqlServer ");
        await Assert.That((string?)await this.Database.ExecuteScalarAsync($"SELECT [Script] FROM [{schema}].[Versions] WHERE [Version] = 1;"))
            .IsEqualTo(SqlServerSchemaInstaller.Scripts[0].For(schema));
    }

    [Test]
    public async Task EnsuringTwiceChangesNothing()
    {
        var schema = NewSchema();

        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);
        var appliedAt = await this.Database.ExecuteScalarAsync($"SELECT [AppliedAt] FROM [{schema}].[Versions] WHERE [Version] = 1;");
        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(1);
        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT [AppliedAt] FROM [{schema}].[Versions] WHERE [Version] = 1;")).IsEqualTo(appliedAt);
    }

    [Test]
    public async Task UpgradeAppliesOnlyTheMissingVersion()
    {
        var schema = NewSchema();
        var version1 = SqlServerSchemaInstaller.Scripts[0];
        var version2 = new SqlServerScript(2, "CREATE TABLE [dbo].[Upgraded] ([Id] INT NOT NULL);\nGO\n");

        await SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, [version1], CancellationToken.None);
        var appliedAt = await this.Database.ExecuteScalarAsync($"SELECT [AppliedAt] FROM [{schema}].[Versions] WHERE [Version] = 1;");
        await SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, [version1, version2], CancellationToken.None);

        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT OBJECT_ID(N'[{schema}].[Upgraded]', N'U');")).IsNotNull();
        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(2);
        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT [AppliedAt] FROM [{schema}].[Versions] WHERE [Version] = 1;")).IsEqualTo(appliedAt);
    }

    [Test]
    public async Task FailedUpgradeRollsBackEntirely()
    {
        var schema = NewSchema();
        var version1 = SqlServerSchemaInstaller.Scripts[0];
        var version2 = new SqlServerScript(2, "CREATE TABLE [dbo].[Partial] ([Id] INT NOT NULL);\nGO\nTHROW 50000, 'Broken upgrade.', 1;\nGO\n");

        await SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, [version1], CancellationToken.None);

        await Assert.That(() => SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, [version1, version2], CancellationToken.None))
            .Throws<SqlException>();
        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT OBJECT_ID(N'[{schema}].[Partial]', N'U');")).IsNull();
        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(1);
    }

    [Test]
    public async Task FailedInstallLeavesNoSchema()
    {
        var schema = NewSchema();
        var broken = new SqlServerScript(1, "CREATE TABLE [dbo].[Partial] ([Id] INT NOT NULL);\nGO\nTHROW 50000, 'Broken install.', 1;\nGO\n");

        await Assert.That(() => SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, [broken], CancellationToken.None))
            .Throws<SqlException>();
        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT SCHEMA_ID(N'{schema}');")).IsNull();
    }

    [Test]
    public async Task ConcurrentCallersApplyEachVersionOnce()
    {
        var schema = NewSchema();

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SqlServerSchema.EnsureAsync(this.ConnectionString, schema)));

        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(1);
    }

    [Test]
    public async Task ThrowsWhenTheSchemaIsNewerThanThePackage()
    {
        var schema = NewSchema();
        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);
        await this.Database.ExecuteScriptAsync($"INSERT INTO [{schema}].[Versions] ([Version]) VALUES (99);");

        await Assert.That(() => SqlServerSchema.EnsureAsync(this.ConnectionString, schema))
            .Throws<PersistenceException>()
            .WithMessageContaining("is at version 99, which is newer than version 1");
    }

    [Test]
    public async Task AdoptsAScriptRunByHand()
    {
        var schema = NewSchema();
        await this.Database.ExecuteScriptAsync($"CREATE SCHEMA [{schema}];");
        await this.Database.ExecuteScriptAsync(SqlServerSchemaInstaller.Scripts[0].For(schema));

        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(1);
        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT [Script] FROM [{schema}].[Versions] WHERE [Version] = 1;")).IsNull();

        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(1);
        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT [Script] FROM [{schema}].[Versions] WHERE [Version] = 1;")).IsNotNull();
    }

    [Test]
    public async Task GetScriptInstallsTheSchema()
    {
        var schema = NewSchema();

        await this.Database.ExecuteScriptAsync(SqlServerSchema.GetScript(schema));
        await this.Database.ExecuteScriptAsync(SqlServerSchema.GetScript(schema));

        foreach (var table in Tables)
        {
            await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT OBJECT_ID(N'[{schema}].[{table}]', N'U');")).IsNotNull();
        }

        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(1);
    }

    [Test]
    public async Task RejectsAnInvalidSchemaName()
    {
        await Assert.That(() => SqlServerSchema.EnsureAsync(this.ConnectionString, "bad name")).Throws<ArgumentException>();
    }

    private static string NewSchema() => string.Concat("s", Guid.NewGuid().ToString("N"));

    private async Task<int> CountVersionsAsync(string schema) =>
        (int)(await this.Database.ExecuteScalarAsync($"SELECT COUNT(*) FROM [{schema}].[Versions];"))!;
}
