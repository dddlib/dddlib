using dddlib.Persistence.SqlServer;
using Microsoft.Data.SqlClient;
using TUnit.Core.Interfaces;

namespace dddlib.Tests.Support;

/// <summary>
/// A database created in the shared container with the dddlib schema scripts applied, dropped on dispose. Inject with
/// <c>[ClassDataSource&lt;SqlServerDatabase&gt;(Shared = SharedType.PerClass)]</c> for one database per test class.
/// </summary>
public sealed class SqlServerDatabase : IAsyncInitializer, IAsyncDisposable
{
    private string? connectionString;

    [ClassDataSource<SqlServerContainer>(Shared = SharedType.PerTestSession)]
    public required SqlServerContainer Container { get; init; }

    public string DatabaseName { get; } = string.Concat("dddlib_", Guid.NewGuid().ToString("N"));

    public string ConnectionString => this.connectionString ?? throw new InvalidOperationException("The database has not been created.");

    public async Task InitializeAsync()
    {
        await ExecuteAsync(this.Container.ConnectionString, $"CREATE DATABASE [{this.DatabaseName}];").ConfigureAwait(false);

        this.connectionString = new SqlConnectionStringBuilder(this.Container.ConnectionString) { InitialCatalog = this.DatabaseName }.ConnectionString;

        await this.RunScriptsAsync("dbo").ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a schema and applies the dddlib scripts to it, for tests that use a schema other than dbo.
    /// </summary>
    public async Task CreateSchemaAsync(string schema)
    {
        await this.ExecuteScriptAsync($"CREATE SCHEMA [{schema}];").ConfigureAwait(false);
        await this.RunScriptsAsync(schema).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a script against the database, batch by batch.
    /// </summary>
    public async Task ExecuteScriptAsync(string script)
    {
        foreach (var batch in SqlServerScripts.SplitBatches(script))
        {
            await ExecuteAsync(this.ConnectionString, batch).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (this.connectionString is null)
        {
            return;
        }

        // Only this database's pool: other test classes are still using theirs in parallel.
        using (var pooled = new SqlConnection(this.connectionString))
        {
            SqlConnection.ClearPool(pooled);
        }

        await ExecuteAsync(
            this.Container.ConnectionString,
            $"ALTER DATABASE [{this.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{this.DatabaseName}];").ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(string connectionString, string commandText)
    {
        await using var connection = new SqlConnection(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await connection.OpenAsync().ConfigureAwait(false);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task RunScriptsAsync(string schema)
    {
        foreach (var name in SqlServerScripts.Names)
        {
            await this.ExecuteScriptAsync(SqlServerScripts.Read(name, schema)).ConfigureAwait(false);
        }
    }
}
