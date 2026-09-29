using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.SqlServer;

public sealed class SqlServerIdentityMap : DefaultIdentityMap
{
    public SqlServerIdentityMap(string connectionString, string schema = "dbo")
        : base(new SqlServerNaturalKeyRepository(connectionString, schema), new DefaultNaturalKeySerializer())
    {
    }
}
