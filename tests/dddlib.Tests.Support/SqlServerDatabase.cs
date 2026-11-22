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
    private static readonly SemaphoreSlim CreateDatabaseGate = new(1, 1);

    private string? connectionString;

    [ClassDataSource<SqlServerContainer>(Shared = SharedType.PerTestSession)]
    public required SqlServerContainer Container { get; init; }

    public string DatabaseName { get; } = string.Concat("dddlib_", Guid.NewGuid().ToString("N"));

    public string ConnectionString => this.connectionString ?? throw new InvalidOperationException("The database has not been created.");

    public async Task InitializeAsync()
    {
        try
        {
            await CreateDatabaseAsync(this.Container.ConnectionString, this.DatabaseName).ConfigureAwait(false);

            this.connectionString = new SqlConnectionStringBuilder(this.Container.ConnectionString) { InitialCatalog = this.DatabaseName }.ConnectionString;

            await SqlServerSchema.EnsureAsync(this.connectionString).ConfigureAwait(false);
        }
        catch (SqlException ex)
        {
            // The message of a failed CREATE DATABASE names only the first error ("Check related errors"): the cause is
            // in the others, and often in the server log.
            var errors = string.Join(Environment.NewLine, ex.Errors.Cast<SqlError>().Select(static error => $"  {error.Number}: {error.Message}"));
            var log = await this.Container.GetLogTailAsync().ConfigureAwait(false);

            throw new InvalidOperationException(
                $"""
                Creating the test database '{this.DatabaseName}' failed. SQL Server reported:
                {errors}
                The end of the server log:
                {log}
                """,
                ex);
        }
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

        // Dropping takes locks in master that a concurrent create or drop also takes, and the two can deadlock. Drops go
        // through the same gate as creates, one at a time, and retry if they are still chosen as a deadlock victim.
        await CreateDatabaseGate.WaitAsync().ConfigureAwait(false);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await ExecuteAsync(
                        this.Container.ConnectionString,
                        $"IF DB_ID(N'{this.DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{this.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{this.DatabaseName}]; END",
                        commandTimeoutSeconds: 120).ConfigureAwait(false);
                    return;
                }
                catch (SqlException ex) when (attempt < 10 && ex.Errors.Cast<SqlError>().Any(static error => error.Number == 1205))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(100, 500))).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            CreateDatabaseGate.Release();
        }
    }

    // CREATE DATABASE copies the model database under an exclusive lock on it. Test classes start together, and dozens of
    // concurrent creates in one container either fail with 1807 ("Could not obtain exclusive lock on database 'model'")
    // or queue past the command timeout, so this process creates its databases one at a time (each test project has its
    // own container). The retry covers a create that still meets another session's lock.
    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await CreateDatabaseGate.WaitAsync().ConfigureAwait(false);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await ExecuteAsync(connectionString, $"CREATE DATABASE [{databaseName}];", commandTimeoutSeconds: 120).ConfigureAwait(false);
                    return;
                }
                catch (SqlException ex) when (attempt < 10 && ex.Errors.Cast<SqlError>().Any(static error => error.Number == 1807))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(100, 500))).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            CreateDatabaseGate.Release();
        }
    }

    private static async Task ExecuteAsync(string connectionString, string commandText, int commandTimeoutSeconds = 30)
    {
        await using var connection = new SqlConnection(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText = commandText;
        await connection.OpenAsync().ConfigureAwait(false);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
