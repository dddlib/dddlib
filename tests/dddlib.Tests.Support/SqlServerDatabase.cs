using dddlib.Persistence.SqlServer;
using Microsoft.Data.SqlClient;
using TUnit.Core.Interfaces;

namespace dddlib.Tests.Support;

/// <summary>
/// A database created in the shared container with the dddlib schema installed, dropped on dispose. Inject with
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

        await SqlServerSchema.EnsureAsync(this.connectionString).ConfigureAwait(false);
    }

    /// <summary>
    /// Installs the dddlib schema in a schema other than dbo.
    /// </summary>
    public Task CreateSchemaAsync(string schema) => SqlServerSchema.EnsureAsync(this.ConnectionString, schema);

    /// <summary>
    /// Runs a script against the database, batch by batch.
    /// </summary>
    public async Task ExecuteScriptAsync(string script)
    {
        foreach (var batch in SqlServerScript.SplitBatches(script))
        {
            await ExecuteAsync(this.ConnectionString, batch).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs a query against the database and returns the first column of the first row.
    /// </summary>
    public async Task<object?> ExecuteScalarAsync(string commandText)
    {
        await using var connection = new SqlConnection(this.ConnectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await connection.OpenAsync().ConfigureAwait(false);
        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return result is DBNull ? null : result;
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
}
