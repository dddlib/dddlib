using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// A memento repository with the identity map on SQL Server and custom storage supplied by the derived class,
/// for example a table shaped for the aggregate root.
/// </summary>
public abstract class SqlServerRepository<T> : Repository<T>
    where T : AggregateRoot
{
    protected SqlServerRepository(string connectionString, string schema = "dbo")
        : base(new SqlServerIdentityMap(connectionString, schema))
    {
        this.ConnectionString = connectionString;
    }

    protected string ConnectionString { get; }
}
