namespace dddlib.Tests.Support;

/// <summary>
/// Base class for feature scenarios that run against SQL Server: a fresh <see cref="dddlib.Runtime.Application"/>
/// per test and one database per scenario class.
/// </summary>
public abstract class SqlServerFeature : Feature
{
    [ClassDataSource<SqlServerDatabase>(Shared = SharedType.PerClass)]
    public required SqlServerDatabase Database { get; init; }

    protected string ConnectionString => this.Database.ConnectionString;
}
