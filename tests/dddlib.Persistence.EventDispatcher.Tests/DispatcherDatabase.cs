using dddlib.Persistence.EventDispatcher.SqlServer;
using dddlib.Tests.Support;
using TUnit.Core.Interfaces;

namespace dddlib.Persistence.EventDispatcher.Tests;

/// <summary>
/// A per-class database with the persistence scripts and the dispatcher script applied.
/// </summary>
public sealed class DispatcherDatabase : IAsyncInitializer
{
    [ClassDataSource<SqlServerDatabase>(Shared = SharedType.PerClass)]
    public required SqlServerDatabase Database { get; init; }

    public string ConnectionString => this.Database.ConnectionString;

    public Task InitializeAsync() => this.Database.ExecuteScriptAsync(SqlServerEventDispatcherScripts.Read());
}
