namespace dddlib.Persistence.SqlServer;

/// <summary>
/// The version of the dddlib SQL Server schema compared with the version this package requires. The package works
/// against a schema at the version it requires or newer, so that processes still running an older package keep working
/// while a newer one upgrades the schema; a schema older than the package requires fails, and so does a schema that has
/// recorded that it no longer supports a package this old.
/// </summary>
/// <param name="Schema">The schema name.</param>
/// <param name="Version">The version the schema is at.</param>
/// <param name="RequiredVersion">The version this package requires, and the latest it can apply.</param>
/// <param name="MinimumRequiredVersion">
/// The oldest required version the schema still supports. It is 1 until a script removes or changes something that
/// older packages use.
/// </param>
public sealed record SqlServerSchemaVersion(string Schema, int Version, int RequiredVersion, int MinimumRequiredVersion)
{
    /// <summary>
    /// Gets a value indicating whether the schema is newer than this package requires: supported, but a sign that this
    /// process should be upgraded.
    /// </summary>
    public bool IsAhead => this.Version > this.RequiredVersion;

    /// <summary>
    /// Gets a value indicating whether this package works against the schema: the schema is at the version the package
    /// requires or newer, and still supports a package that requires that version.
    /// </summary>
    public bool IsCompatible => this.Version >= this.RequiredVersion && this.RequiredVersion >= this.MinimumRequiredVersion;
}
