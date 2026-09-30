namespace dddlib.Persistence.EventDispatcher.SqlServer;

/// <summary>
/// The version of the dddlib SQL Server schema compared with the version this package requires. The package works
/// against a database at its own version or newer, so that processes still running older code keep working while a
/// newer one upgrades the schema; a database older than the package fails.
/// </summary>
/// <param name="Schema">The schema name.</param>
/// <param name="DatabaseVersion">The version the database is at.</param>
/// <param name="CodeVersion">The version this package requires, and the latest it can apply.</param>
public sealed record SqlServerEventDispatcherSchemaVersion(string Schema, int DatabaseVersion, int CodeVersion)
{
    /// <summary>
    /// Gets a value indicating whether the database is newer than this package: supported, but a sign that this process
    /// should be upgraded.
    /// </summary>
    public bool IsDatabaseAhead => this.DatabaseVersion > this.CodeVersion;
}
