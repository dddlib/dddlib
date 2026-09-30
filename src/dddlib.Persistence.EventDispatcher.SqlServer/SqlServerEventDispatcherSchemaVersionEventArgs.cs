namespace dddlib.Persistence.EventDispatcher.SqlServer;

public sealed class SqlServerEventDispatcherSchemaVersionEventArgs(SqlServerEventDispatcherSchemaVersion version) : EventArgs
{
    public SqlServerEventDispatcherSchemaVersion Version { get; } = version;
}
