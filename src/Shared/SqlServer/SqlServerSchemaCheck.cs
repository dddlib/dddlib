using System.Collections.Concurrent;
using System.Globalization;
using System.Transactions;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// Fails loudly when the schema is older than this assembly requires, instead of with a SQL error about a missing
/// object. The version is read once per connection string and schema; only a schema that is current is remembered, so
/// a process recovers without a restart once the schema has been upgraded.
/// </summary>
internal static class SqlServerSchemaCheck
{
    private static readonly ConcurrentDictionary<(string ConnectionString, string Schema), bool> CurrentSchemas = new();

    public static ValueTask EnsureCurrentAsync(string connectionString, string quotedSchema, CancellationToken cancellationToken) =>
        CurrentSchemas.ContainsKey((connectionString, quotedSchema))
            ? ValueTask.CompletedTask
            : new ValueTask(CheckAsync(connectionString, quotedSchema, cancellationToken));

    private static async Task CheckAsync(string connectionString, string quotedSchema, CancellationToken cancellationToken)
    {
        int version;
        using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            version = await SqlServerSchemaInstaller.ReadVersionAsync(connection, quotedSchema, cancellationToken).ConfigureAwait(false);
        }

        if (version < SqlServerSchemaInstaller.RequiredVersion)
        {
            throw new PersistenceException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The SQL Server schema {0} is at version {1}, but {2} requires version {3}.
To fix this issue, either:
- call SqlServerSchema.EnsureAsync (dddlib.Persistence.SqlServer) or SqlServerEventDispatcherSchema.EnsureAsync (dddlib.Persistence.EventDispatcher.SqlServer) with the connection string and schema, from a migration step or at startup, or
- run the scripts from the package's content/Scripts folder, in version order, against the schema.
Further information: https://github.com/dddlib/dddlib/blob/main/docs/persistence/sql-server.md",
                    quotedSchema,
                    version,
                    SqlServerSchemaInstaller.Description,
                    SqlServerSchemaInstaller.RequiredVersion));
        }

        CurrentSchemas.TryAdd((connectionString, quotedSchema), true);
    }
}
