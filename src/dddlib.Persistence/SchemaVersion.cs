namespace dddlib.Persistence;

/// <summary>
/// The version of a persistence schema compared with the version the code requires. Code works against a database at
/// its own version or newer, so that processes still running older code keep working while a newer one upgrades the
/// schema; code ahead of the database fails.
/// </summary>
/// <param name="Schema">The schema name.</param>
/// <param name="DatabaseVersion">The version the database is at.</param>
/// <param name="CodeVersion">The version the code requires, and the latest it can apply.</param>
public sealed record SchemaVersion(string Schema, int DatabaseVersion, int CodeVersion)
{
    /// <summary>
    /// Gets a value indicating whether the database is newer than the code: supported, but a sign that this process
    /// should be upgraded.
    /// </summary>
    public bool IsDatabaseAhead => this.DatabaseVersion > this.CodeVersion;
}
