using Testcontainers.MsSql;
using TUnit.Core.Interfaces;

namespace dddlib.Tests.Support;

/// <summary>
/// One SQL Server container for the whole test session. Inject with
/// <c>[ClassDataSource&lt;SqlServerContainer&gt;(Shared = SharedType.PerTestSession)]</c>. Requires Docker.
/// </summary>
public sealed class SqlServerContainer : IAsyncInitializer, IAsyncDisposable
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private MsSqlContainer? container;

    /// <summary>
    /// Gets the connection string to the container's master database.
    /// </summary>
    public string ConnectionString => (this.container ?? throw new InvalidOperationException("The container has not been started.")).GetConnectionString();

    public async Task InitializeAsync()
    {
        this.container = new MsSqlBuilder(Image).Build();
        await this.container.StartAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (this.container is not null)
        {
            await this.container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
