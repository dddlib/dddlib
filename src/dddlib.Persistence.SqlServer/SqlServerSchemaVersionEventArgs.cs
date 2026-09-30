namespace dddlib.Persistence.SqlServer;

public sealed class SqlServerSchemaVersionEventArgs(SqlServerSchemaVersion version) : EventArgs
{
    public SqlServerSchemaVersion Version { get; } = version;
}
