using dddlib.Persistence.Projections.SqlServer;
using dddlib.Tests.Support;
using TUnit.Core.Interfaces;

namespace dddlib.Persistence.Projections.Tests;

/// <summary>
/// A per-class database with the dddlib schema installed, as the projections package installs it.
/// </summary>
public sealed class ProjectionsDatabase : IAsyncInitializer
{
    [ClassDataSource<SqlServerDatabase>(Shared = SharedType.PerClass)]
    public required SqlServerDatabase Database { get; init; }

    public string ConnectionString => this.Database.ConnectionString;

    public Task InitializeAsync() => SqlServerProjectionsSchema.EnsureAsync(this.Database.ConnectionString);
}
