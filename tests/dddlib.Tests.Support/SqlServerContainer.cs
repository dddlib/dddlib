using Testcontainers.MsSql;
using TUnit.Core.Interfaces;

namespace dddlib.Tests.Support;

/// <summary>
/// One SQL Server for the whole test session: the server named by the <c>DDDLIB_TEST_SQLSERVER</c> environment
/// variable, or otherwise a container started for the session. Inject with
/// <c>[ClassDataSource&lt;SqlServerContainer&gt;(Shared = SharedType.PerTestSession)]</c>. A container requires Docker.
/// </summary>
/// <remarks>
/// A test session is one test process, and <c>dotnet test --solution</c> runs each test project in a process of its
/// own, so each project starts a container. To share one server between them, start it once and set
/// <c>DDDLIB_TEST_SQLSERVER</c> to a connection string for its master database with permission to create databases.
/// Every test class creates its own database, so test projects can share a server.
/// </remarks>
public sealed class SqlServerContainer : IAsyncInitializer, IAsyncDisposable
{
    public const string ConnectionStringVariable = "DDDLIB_TEST_SQLSERVER";

    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private MsSqlContainer? container;
    private string? connectionString;

    /// <summary>
    /// Gets the connection string to the server's master database.
    /// </summary>
    public string ConnectionString => this.connectionString ?? throw new InvalidOperationException("The server has not been started.");

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (!string.IsNullOrWhiteSpace(external))
        {
            this.connectionString = external;
            return;
        }

        this.container = new MsSqlBuilder(Image).Build();
        await this.container.StartAsync().ConfigureAwait(false);
        this.connectionString = this.container.GetConnectionString();
    }

    /// <summary>
    /// Gets the end of the server's log, to explain a failure, or a note that it is not available for a server this
    /// session did not start.
    /// </summary>
    public async Task<string> GetLogTailAsync(int lines = 50)
    {
        if (this.container is null)
        {
            return $"The server log is not available: the server was not started by the tests ({ConnectionStringVariable} is set).";
        }

        var (stdout, stderr) = await this.container.GetLogsAsync().ConfigureAwait(false);
        var log = string.Concat(stdout, stderr).Split('\n');

        return string.Join('\n', log.Skip(Math.Max(0, log.Length - lines)));
    }

    public async ValueTask DisposeAsync()
    {
        if (this.container is not null)
        {
            await this.container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
