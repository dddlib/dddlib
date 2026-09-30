namespace dddlib.Persistence.SqlServer;

/// <summary>
/// Creates or upgrades the dddlib SQL Server schema. Nothing in the library changes the schema implicitly: call
/// <see cref="EnsureAsync"/> from a migration step or at startup, under a credential allowed to run DDL, or run the
/// text of <see cref="GetScript"/> with a migration tool.
/// </summary>
public static class SqlServerSchema
{
    /// <summary>
    /// Creates the schema if it does not exist and applies the versions it is missing, in one transaction. Concurrent
    /// callers wait for each other, so it is safe to call from every instance at startup. A schema ahead of this
    /// package is left as it is and reported by <see cref="SqlServerSchemaVersion.IsAhead"/>, unless it has recorded
    /// that it no longer supports this package, which fails with a <see cref="PersistenceException"/>.
    /// </summary>
    public static async Task<SqlServerSchemaVersion> EnsureAsync(string connectionString, string schema = "dbo", CancellationToken cancellationToken = default)
    {
        var (version, requiredVersion, minimumRequiredVersion) = await SqlServerSchemaInstaller.EnsureAsync(connectionString, schema, cancellationToken).ConfigureAwait(false);
        return new SqlServerSchemaVersion(schema, version, requiredVersion, minimumRequiredVersion);
    }

    /// <summary>
    /// Reads the version of the schema without changing anything, for a startup log or a health check in a process
    /// that does not upgrade the schema itself. A schema that is not installed is at version 0. Unlike
    /// <see cref="EnsureAsync"/> it does not fail when this package cannot use the schema: see
    /// <see cref="SqlServerSchemaVersion.IsCompatible"/>.
    /// </summary>
    public static async Task<SqlServerSchemaVersion> GetVersionAsync(string connectionString, string schema = "dbo", CancellationToken cancellationToken = default)
    {
        var (version, requiredVersion, minimumRequiredVersion) = await SqlServerSchemaInstaller.GetVersionAsync(connectionString, schema, cancellationToken).ConfigureAwait(false);
        return new SqlServerSchemaVersion(schema, version, requiredVersion, minimumRequiredVersion);
    }

    /// <summary>
    /// Gets the whole schema as one idempotent script for the schema, with batches separated by <c>GO</c>.
    /// </summary>
    public static string GetScript(string schema = "dbo") => SqlServerSchemaInstaller.GetScript(schema);
}
