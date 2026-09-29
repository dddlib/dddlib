using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// An event store repository backed by SQL Server. The schema must have been created with the shipped scripts.
/// </summary>
public sealed class SqlServerEventStoreRepository : EventStoreRepository
{
    public SqlServerEventStoreRepository(string connectionString, string schema = "dbo")
        : base(
            new SqlServerIdentityMap(connectionString, schema),
            new SqlServerEventStore(connectionString, schema),
            new SqlServerSnapshotStore(connectionString, schema))
    {
    }
}
