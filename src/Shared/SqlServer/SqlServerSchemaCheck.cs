using System.Collections.Concurrent;
using System.Globalization;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// Fails loudly when the schema is older than this assembly requires, instead of with a SQL error about a missing
/// object. A newer schema is accepted, so that older code keeps working during a rolling upgrade, unless a script has
/// recorded that the schema no longer supports a package this old. The version is read once per connection string and
/// schema; only a compatible schema is remembered, so a process recovers without a restart once the schema has been
/// upgraded.
/// </summary>
internal static class SqlServerSchemaCheck
{
    private static readonly ConcurrentDictionary<(string ConnectionString, string Schema), bool> CompatibleSchemas = new();

    public static ValueTask EnsureCompatibleAsync(string connectionString, string quotedSchema, CancellationToken cancellationToken) =>
        CompatibleSchemas.ContainsKey((connectionString, quotedSchema))
            ? ValueTask.CompletedTask
            : new ValueTask(CheckAsync(connectionString, quotedSchema, cancellationToken));

    private static async Task CheckAsync(string connectionString, string quotedSchema, CancellationToken cancellationToken)
    {
        var (version, minimumRequiredVersion) = await SqlServerSchemaInstaller.ReadVersionAsync(connectionString, quotedSchema, cancellationToken).ConfigureAwait(false);
        if (version < SqlServerSchemaInstaller.RequiredVersion)
        {
            throw new PersistenceException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The SQL Server schema {0} is at version {1}, but {2} requires version {3}.
To fix this issue, either:
- call SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer), SqlServerEventDispatcherSchema.EnsureAsync (dddlib.Persistence.EventDispatcher.SqlServer) or SqlServerProjectionsSchema.EnsureAsync (dddlib.Persistence.Projections.SqlServer) with the connection string and schema, from a migration step or at startup, or
- run the scripts from the package's content/Scripts folder, in version order, against the schema.
Further information: https://github.com/dddlib/dddlib/blob/main/docs/persistence/sql-server.md",
                    quotedSchema,
                    version,
                    SqlServerSchemaInstaller.Description,
                    SqlServerSchemaInstaller.RequiredVersion));
        }

        SqlServerSchemaInstaller.ThrowIfTooOld(quotedSchema, version, minimumRequiredVersion, SqlServerSchemaInstaller.RequiredVersion);

        CompatibleSchemas.TryAdd((connectionString, quotedSchema), true);
    }
}
