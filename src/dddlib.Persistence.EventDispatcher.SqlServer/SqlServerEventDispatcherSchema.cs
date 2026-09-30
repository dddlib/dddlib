using dddlib.Persistence.SqlServer;

namespace dddlib.Persistence.EventDispatcher.SqlServer;

/// <summary>
/// Creates or upgrades the dddlib SQL Server schema, the same schema as <c>SqlServerSchema</c> in
/// dddlib.Persistence.SqlServer: it includes the event store the dispatcher reads. Nothing in the library changes the
/// schema implicitly: call <see cref="EnsureAsync"/> from a migration step or at startup, under a credential allowed to
/// run DDL, or run the text of <see cref="GetScript"/> with a migration tool.
/// </summary>
public static class SqlServerEventDispatcherSchema
{
    /// <summary>
    /// Creates the schema if it does not exist and applies the versions it is missing, in one transaction. Concurrent
    /// callers wait for each other, so it is safe to call from every instance at startup. A database ahead of this
    /// package is left as it is and reported by <see cref="SqlServerEventDispatcherSchemaVersion.IsDatabaseAhead"/>.
    /// </summary>
    public static async Task<SqlServerEventDispatcherSchemaVersion> EnsureAsync(string connectionString, string schema = "dbo", CancellationToken cancellationToken = default)
    {
        var (databaseVersion, codeVersion) = await SqlServerSchemaInstaller.EnsureAsync(connectionString, schema, cancellationToken).ConfigureAwait(false);
        return new SqlServerEventDispatcherSchemaVersion(schema, databaseVersion, codeVersion);
    }

    /// <summary>
    /// Gets the whole schema as one idempotent script for the schema, with batches separated by <c>GO</c>.
    /// </summary>
    public static string GetScript(string schema = "dbo") => SqlServerSchemaInstaller.GetScript(schema);
}
