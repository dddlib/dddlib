using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.Tests.Integration;

// Each test installs into a schema of its own, so it starts from nothing inside the per-class database.
public class SqlServerSchemaTests : SqlServerIntegration
{
    private static readonly string[] Tables = ["Types", "NaturalKeys", "Streams", "Events", "Snapshots", "Mementos", "Batches", "DispatchedEvents", "Projections", "ProjectionViews", "Versions"];

    [Test]
    public async Task CreatesTheSchemaWithEveryObject()
    {
        var schema = NewSchema();

        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        foreach (var table in Tables)
        {
            await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT OBJECT_ID(N'[{schema}].[{table}]', N'U');")).IsNotNull();
        }

        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(RequiredVersion);
        foreach (var script in SqlServerSchemaInstaller.Scripts)
        {
            await Assert.That((string?)await this.Database.ExecuteScalarAsync($"SELECT [Description] FROM [{schema}].[Versions] WHERE [Version] = {script.Version};"))
                .StartsWith("dddlib.Persistence.SqlServer ");
            await Assert.That((string?)await this.Database.ExecuteScalarAsync($"SELECT [Script] FROM [{schema}].[Versions] WHERE [Version] = {script.Version};"))
                .IsEqualTo(script.For(schema));
        }
    }

    [Test]
    public async Task EnsuringTwiceChangesNothing()
    {
        var schema = NewSchema();

        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);
        var appliedAt = await this.Database.ExecuteScalarAsync($"SELECT [AppliedAt] FROM [{schema}].[Versions] WHERE [Version] = 1;");
        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(RequiredVersion);
        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT [AppliedAt] FROM [{schema}].[Versions] WHERE [Version] = 1;")).IsEqualTo(appliedAt);
    }

    [Test]
    public async Task EnsuringACurrentSchemaDoesNotWaitForTheUpgradeLock()
    {
        var schema = NewSchema();
        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        // Another instance holds the upgrade lock, as it does for as long as it takes to apply a newer script.
        await using var connection = new SqlConnection(this.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"EXEC sp_getapplock @Resource = N'dddlib.Schema.{schema}', @LockMode = 'Exclusive', @LockOwner = 'Transaction';";
            await command.ExecuteNonQueryAsync();
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var version = await SqlServerSchema.EnsureAsync(this.ConnectionString, schema, timeout.Token);

        await Assert.That(version).IsEqualTo(new SqlServerSchemaVersion(schema, RequiredVersion, RequiredVersion, 1));
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

        await Assert.That(() => (Task)SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, [version1, version2], CancellationToken.None))
            .Throws<SqlException>();
        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT OBJECT_ID(N'[{schema}].[Partial]', N'U');")).IsNull();
        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(1);
    }

    [Test]
    public async Task FailedInstallLeavesNoSchema()
    {
        var schema = NewSchema();
        var broken = new SqlServerScript(1, "CREATE TABLE [dbo].[Partial] ([Id] INT NOT NULL);\nGO\nTHROW 50000, 'Broken install.', 1;\nGO\n");

        await Assert.That(() => (Task)SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, [broken], CancellationToken.None))
            .Throws<SqlException>();
        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT SCHEMA_ID(N'{schema}');")).IsNull();
    }

    [Test]
    public async Task ConcurrentCallersApplyEachVersionOnce()
    {
        var schema = NewSchema();

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SqlServerSchema.EnsureAsync(this.ConnectionString, schema)));

        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(RequiredVersion);
    }

    [Test]
    public async Task ReportsTheVersionOfACurrentSchema()
    {
        var schema = NewSchema();

        var installed = await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);
        var ensuredAgain = await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        await Assert.That(installed).IsEqualTo(new SqlServerSchemaVersion(schema, RequiredVersion, RequiredVersion, 1));
        await Assert.That(installed.IsAhead).IsFalse();
        await Assert.That(ensuredAgain).IsEqualTo(installed);
    }

    [Test]
    public async Task ReportsWhenTheSchemaIsAheadOfThePackage()
    {
        var schema = NewSchema();
        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);
        await this.Database.ExecuteScriptAsync($"INSERT INTO [{schema}].[Versions] ([Version]) VALUES (99);");

        var version = await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        await Assert.That(version).IsEqualTo(new SqlServerSchemaVersion(schema, 99, RequiredVersion, 1));
        await Assert.That(version.IsAhead).IsTrue();
        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(RequiredVersion + 1);
    }

    [Test]
    public async Task OlderCodeKeepsWorkingAfterNewerCodeUpgrades()
    {
        // Process A, on a newer package, upgrades the schema; process B, on this package, then runs its first command
        // against it and later restarts.
        var schema = NewSchema();
        var next = new SqlServerScript(RequiredVersion + 1, "CREATE TABLE [dbo].[Upgraded] ([Id] INT NOT NULL);\nGO\n");
        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        var processA = await SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, [.. SqlServerSchemaInstaller.Scripts, next], CancellationToken.None);
        var stream = await new SqlServerEventStore(this.ConnectionString, schema).GetStreamAsync(Guid.NewGuid(), 0);
        var processB = await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        await Assert.That(processA).IsEqualTo((RequiredVersion + 1, RequiredVersion + 1, 1));
        await Assert.That(stream.Events).IsEmpty();
        await Assert.That(processB).IsEqualTo(new SqlServerSchemaVersion(schema, RequiredVersion + 1, RequiredVersion, 1));
        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(RequiredVersion + 1);
    }

    [Test]
    public async Task OlderCodeFailsLoudlyAfterAContractingUpgrade()
    {
        // Process A, on a newer package, applies a script that removes something this package uses and records the
        // oldest required version that still works; process B, on this package, is now too old.
        var schema = NewSchema();
        var contracting = new SqlServerScript(
            RequiredVersion + 1,
            $"DROP PROCEDURE [dbo].[GetStream];\nGO\nINSERT INTO [dbo].[Versions] ([Version], [MinimumRequiredVersion]) VALUES ({RequiredVersion + 1}, {RequiredVersion + 1});\nGO\n");
        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        var processA = await SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, [.. SqlServerSchemaInstaller.Scripts, contracting], CancellationToken.None);

        await Assert.That(processA).IsEqualTo((RequiredVersion + 1, RequiredVersion + 1, RequiredVersion + 1));
        await Assert.That(() => (Task)new SqlServerEventStore(this.ConnectionString, schema).GetStreamAsync(Guid.NewGuid(), 0))
            .Throws<PersistenceException>()
            .WithMessageContaining($"The SQL Server schema [{schema}] is at version {RequiredVersion + 1}, which supports packages that require version {RequiredVersion + 1} or later, but dddlib.Persistence.SqlServer ");
        await Assert.That(() => (Task)SqlServerSchema.EnsureAsync(this.ConnectionString, schema))
            .Throws<PersistenceException>()
            .WithMessageContaining("To fix this issue");
        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(RequiredVersion + 1);
    }

    [Test]
    public async Task GetVersionReadsWithoutChangingAnything()
    {
        var schema = NewSchema();

        var missing = await SqlServerSchema.GetVersionAsync(this.ConnectionString, schema);
        var schemaId = await this.Database.ExecuteScalarAsync($"SELECT SCHEMA_ID(N'{schema}');");
        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);
        var current = await SqlServerSchema.GetVersionAsync(this.ConnectionString, schema);

        await Assert.That(missing).IsEqualTo(new SqlServerSchemaVersion(schema, 0, RequiredVersion, 1));
        await Assert.That(missing.IsCompatible).IsFalse();
        await Assert.That(schemaId).IsNull();
        await Assert.That(current).IsEqualTo(new SqlServerSchemaVersion(schema, RequiredVersion, RequiredVersion, 1));
        await Assert.That(current.IsCompatible).IsTrue();
        await Assert.That(current.IsAhead).IsFalse();
    }

    [Test]
    public async Task GetVersionReportsASchemaThatIsAheadOrNoLongerSupportsThePackage()
    {
        var schema = NewSchema();
        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

        await this.Database.ExecuteScriptAsync($"INSERT INTO [{schema}].[Versions] ([Version]) VALUES (98);");
        var ahead = await SqlServerSchema.GetVersionAsync(this.ConnectionString, schema);
        await this.Database.ExecuteScriptAsync($"INSERT INTO [{schema}].[Versions] ([Version], [MinimumRequiredVersion]) VALUES (99, 98);");
        var unsupported = await SqlServerSchema.GetVersionAsync(this.ConnectionString, schema);

        await Assert.That(ahead).IsEqualTo(new SqlServerSchemaVersion(schema, 98, RequiredVersion, 1));
        await Assert.That(ahead.IsAhead).IsTrue();
        await Assert.That(ahead.IsCompatible).IsTrue();
        await Assert.That(unsupported).IsEqualTo(new SqlServerSchemaVersion(schema, 99, RequiredVersion, 98));
        await Assert.That(unsupported.IsAhead).IsTrue();
        await Assert.That(unsupported.IsCompatible).IsFalse();
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

        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(RequiredVersion);
        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT [Script] FROM [{schema}].[Versions] WHERE [Version] = 1;")).IsNotNull();
    }

    [Test]
    public async Task UpgradesFromVersion1()
    {
        var schema = NewSchema();
        await SqlServerSchemaInstaller.EnsureAsync(this.ConnectionString, schema, [SqlServerSchemaInstaller.Scripts[0]], CancellationToken.None);
        var before = await SqlServerSchema.GetVersionAsync(this.ConnectionString, schema);

        var upgraded = await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);
        var page = await new SqlServerEventStore(this.ConnectionString, schema).ReadEventsAsync(0, 10);

        await Assert.That(before).IsEqualTo(new SqlServerSchemaVersion(schema, 1, RequiredVersion, 1));
        await Assert.That(before.IsCompatible).IsFalse();
        await Assert.That(upgraded).IsEqualTo(new SqlServerSchemaVersion(schema, RequiredVersion, RequiredVersion, 1));
        await Assert.That(await this.Database.ExecuteScalarAsync($"SELECT OBJECT_ID(N'[{schema}].[ReadEvents]', N'P');")).IsNotNull();
        await Assert.That(page.Events).IsEmpty();
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

        await Assert.That(await this.CountVersionsAsync(schema)).IsEqualTo(RequiredVersion);
    }

    [Test]
    public async Task FailsLoudlyWhenTheSchemaIsBehind()
    {
        var schema = NewSchema();
        await this.Database.ExecuteScriptAsync($"CREATE SCHEMA [{schema}];");
        var eventStore = new SqlServerEventStore(this.ConnectionString, schema);

        await Assert.That(() => (Task)eventStore.GetStreamAsync(Guid.NewGuid(), 0))
            .Throws<PersistenceException>()
            .WithMessageContaining($"The SQL Server schema [{schema}] is at version 0, but dddlib.Persistence.SqlServer ");
        await Assert.That(() => new SqlServerIdentityMap(this.ConnectionString, schema).TryGetAsync(typeof(object), typeof(string), "key"))
            .Throws<PersistenceException>()
            .WithMessageContaining("To fix this issue");

        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);
        var stream = await eventStore.GetStreamAsync(Guid.NewGuid(), 0);

        await Assert.That(stream.Events).IsEmpty();
    }

    [Test]
    public async Task FailsLoudlyWhenTheVersionIsMissing()
    {
        var schema = NewSchema();
        await SqlServerSchema.EnsureAsync(this.ConnectionString, schema);
        await this.Database.ExecuteScriptAsync($"DELETE FROM [{schema}].[Versions];");

        await Assert.That(() => new SqlServerSnapshotStore(this.ConnectionString, schema).GetSnapshotAsync(Guid.NewGuid()))
            .Throws<PersistenceException>()
            .WithMessageContaining("is at version 0");
    }

    [Test]
    public async Task RejectsAnInvalidSchemaName()
    {
        await Assert.That(() => (Task)SqlServerSchema.EnsureAsync(this.ConnectionString, "bad name")).Throws<ArgumentException>();
        await Assert.That(() => (Task)SqlServerSchema.GetVersionAsync(this.ConnectionString, "bad name")).Throws<ArgumentException>();
    }

    private static int RequiredVersion => SqlServerSchemaInstaller.RequiredVersion;

    private static string NewSchema() => string.Concat("s", Guid.NewGuid().ToString("N"));

    private async Task<int> CountVersionsAsync(string schema) =>
        (int)(await this.Database.ExecuteScalarAsync($"SELECT COUNT(*) FROM [{schema}].[Versions];"))!;
}
