namespace dddlib.Persistence.EventDispatcher.SqlServer;

/// <summary>
/// An event dispatcher over the events committed to a SQL Server event store. The schema must have been created
/// with the shipped scripts, including this package's.
/// </summary>
public sealed class SqlServerEventDispatcher : Sdk.EventDispatcher
{
    public SqlServerEventDispatcher(string connectionString, IEventDispatcher dispatcher, EventDispatcherOptions? options = null, string schema = "dbo")
        : base(dispatcher, new SqlServerEventBatchStore(connectionString, schema), options)
    {
    }

    public SqlServerEventDispatcher(string connectionString, Action<long, object> dispatch, EventDispatcherOptions? options = null, string schema = "dbo")
        : this(connectionString, new CustomEventDispatcher(dispatch), options, schema)
    {
    }
}
