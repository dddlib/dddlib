namespace dddlib.Persistence.SqlServer;

/// <summary>
/// Creates or upgrades the dddlib SQL Server schema. Nothing in the library changes the schema implicitly: call
/// <see cref="EnsureAsync"/> from a migration step or at startup, under a credential allowed to run DDL, or run the
/// text of <see cref="GetScript"/> with a migration tool.
/// </summary>
public static class SqlServerSchema
{
    static SqlServerSchema()
    {
        SqlServerSchemaCheck.DatabaseAhead += static (schema, databaseVersion, codeVersion) =>
            DatabaseAhead?.Invoke(null, new SqlServerSchemaVersionEventArgs(new SqlServerSchemaVersion(schema, databaseVersion, codeVersion)));
    }

    /// <summary>
    /// Creates the schema if it does not exist and applies the versions it is missing, in one transaction. Concurrent
    /// callers wait for each other, so it is safe to call from every instance at startup. A database ahead of this
    /// package is left as it is, reported in the result and through <see cref="DatabaseAhead"/>.
    /// </summary>
    public static async Task<SqlServerSchemaVersion> EnsureAsync(string connectionString, string schema = "dbo", CancellationToken cancellationToken = default)
    {
        var (databaseVersion, codeVersion) = await SqlServerSchemaInstaller.EnsureAsync(connectionString, schema, cancellationToken).ConfigureAwait(false);
        return new SqlServerSchemaVersion(schema, databaseVersion, codeVersion);
    }

    /// <summary>
    /// Gets the whole schema as one idempotent script for the schema, with batches separated by <c>GO</c>.
    /// </summary>
    public static string GetScript(string schema = "dbo") => SqlServerSchemaInstaller.GetScript(schema);

    /// <summary>
    /// Raised when the database is ahead of this package: by <see cref="EnsureAsync"/>, and by the first command of a
    /// SQL Server class per connection string and schema. The database keeps working; log it and upgrade this process.
    /// </summary>
    public static event EventHandler<SqlServerSchemaVersionEventArgs>? DatabaseAhead;
}
