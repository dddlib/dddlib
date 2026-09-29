namespace dddlib.Tests.Support;

/// <summary>
/// Base class for integration tests that run against SQL Server with one database per test class.
/// </summary>
public abstract class SqlServerIntegration
{
    [ClassDataSource<SqlServerDatabase>(Shared = SharedType.PerClass)]
    public required SqlServerDatabase Database { get; init; }

    protected string ConnectionString => this.Database.ConnectionString;
}
