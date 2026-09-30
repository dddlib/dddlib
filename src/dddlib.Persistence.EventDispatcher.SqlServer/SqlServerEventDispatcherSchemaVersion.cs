namespace dddlib.Persistence.EventDispatcher.SqlServer;

/// <summary>
/// The version of the dddlib SQL Server schema compared with the version this package requires. The package works
/// against a schema at the version it requires or newer, so that processes still running an older package keep working
/// while a newer one upgrades the schema; a schema older than the package requires fails.
/// </summary>
/// <param name="Schema">The schema name.</param>
/// <param name="Version">The version the schema is at.</param>
/// <param name="RequiredVersion">The version this package requires, and the latest it can apply.</param>
public sealed record SqlServerEventDispatcherSchemaVersion(string Schema, int Version, int RequiredVersion)
{
    /// <summary>
    /// Gets a value indicating whether the schema is newer than this package requires: supported, but a sign that this
    /// process should be upgraded.
    /// </summary>
    public bool IsAhead => this.Version > this.RequiredVersion;
}
