using System.Data;
using System.Globalization;
using System.Text.Json;
using dddlib.Persistence.SqlServer;
using dddlib.Sdk;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;

namespace dddlib.Persistence.Projections.SqlServer;

/// <summary>
/// The projection procedures, each run on a given connection and optional transaction, shared by the repository, the
/// key/value store and the user's-own-tables projection.
/// </summary>
internal static class SqlServerProjectionCommands
{
    /// <summary>
    /// The longest key the <c>ProjectionViews</c> table holds: its clustered key, with the projection id, must stay
    /// within 900 bytes.
    /// </summary>
    public const int MaxKeyLength = 400;

    private static readonly SqlMetaData[] ViewColumns =
    [
        new("Key", SqlDbType.NVarChar, MaxKeyLength),
        new("Payload", SqlDbType.NVarChar, -1),
    ];

    public static string SerializeKey<TIdentity>(TIdentity identity)
        where TIdentity : notnull
    {
        ArgumentNullException.ThrowIfNull(identity);

        var key = JsonSerializer.Serialize(identity, JsonSerialization.Options);
        if (key.Length > MaxKeyLength)
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "The serialized identity is {0} characters long; a projection view key may be at most {1}.", key.Length, MaxKeyLength),
                nameof(identity));
        }

        return key;
    }

    public static TIdentity DeserializeKey<TIdentity>(string key)
        where TIdentity : notnull =>
        JsonSerializer.Deserialize<TIdentity>(key, JsonSerialization.Options)
            ?? throw new PersistenceException("A projection view key deserialized to null.");

    public static string SerializeView<TEntity>(TEntity entity)
        where TEntity : class =>
        JsonSerializer.Serialize(entity, JsonSerialization.Options);

    public static TEntity DeserializeView<TEntity>(string payload)
        where TEntity : class =>
        JsonSerializer.Deserialize<TEntity>(payload, JsonSerialization.Options)
            ?? throw new PersistenceException("A projection view deserialized to null.");

    /// <summary>
    /// Builds the <c>ProjectionViewList</c> rows: a view to add or replace, or a null payload to remove.
    /// </summary>
    public static List<SqlDataRecord> ToRecords<TIdentity, TEntity>(IEnumerable<KeyValuePair<TIdentity, TEntity?>> changes)
        where TIdentity : notnull
        where TEntity : class
    {
        var records = new List<SqlDataRecord>();
        foreach (var change in changes)
        {
            var record = new SqlDataRecord(ViewColumns);
            record.SetString(0, SerializeKey(change.Key));
            if (change.Value is null)
            {
                record.SetDBNull(1);
            }
            else
            {
                record.SetString(1, SerializeView(change.Value));
            }

            records.Add(record);
        }

        return records;
    }

    public static async Task<long> GetCheckpointAsync(SqlConnection connection, SqlTransaction? transaction, string quotedSchema, string name, CancellationToken cancellationToken)
    {
        await using var command = Create(connection, transaction, quotedSchema, "GetProjectionCheckpoint", name);

        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>
    /// Moves the checkpoint, or throws <see cref="ConcurrencyException"/> when it is not at the expected value. Call it
    /// first in the transaction that writes the page.
    /// </summary>
    public static async Task AdvanceCheckpointAsync(SqlConnection connection, SqlTransaction transaction, string quotedSchema, string name, long expectedCheckpoint, long checkpoint, CancellationToken cancellationToken)
    {
        await using var command = Create(connection, transaction, quotedSchema, "AdvanceProjectionCheckpoint", name);
        command.Parameters.Add("@ExpectedCheckpoint", SqlDbType.BigInt).Value = expectedCheckpoint;
        command.Parameters.Add("@Checkpoint", SqlDbType.BigInt).Value = checkpoint;

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Has(SqlServerErrors.CommitStateMismatch))
        {
            throw new ConcurrencyException(
                string.Format(CultureInfo.InvariantCulture, "The checkpoint of the projection '{0}' is no longer {1}: another runner applied the page first.", name, expectedCheckpoint),
                ex);
        }
    }

    public static async Task ResetCheckpointAsync(SqlConnection connection, SqlTransaction transaction, string quotedSchema, string name, CancellationToken cancellationToken)
    {
        await using var command = Create(connection, transaction, quotedSchema, "ResetProjectionCheckpoint", name);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves the rows in one round trip. Does nothing for no rows: SqlClient rejects an empty table-valued parameter.
    /// </summary>
    public static async Task SaveViewsAsync(SqlConnection connection, SqlTransaction? transaction, string quotedSchema, string name, List<SqlDataRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return;
        }

        await using var command = Create(connection, transaction, quotedSchema, "SaveProjectionViews", name);
        var parameter = command.Parameters.Add("@Views", SqlDbType.Structured);
        parameter.TypeName = string.Concat(quotedSchema, ".[ProjectionViewList]");
        parameter.Value = records;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task DeleteViewsAsync(SqlConnection connection, SqlTransaction? transaction, string quotedSchema, string name, CancellationToken cancellationToken)
    {
        await using var command = Create(connection, transaction, quotedSchema, "DeleteProjectionViews", name);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static SqlCommand Create(SqlConnection connection, SqlTransaction? transaction, string quotedSchema, string procedure, string name)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(quotedSchema, ".[", procedure, "]");
        command.Parameters.Add("@Name", SqlDbType.VarChar, 511).Value = name;
        return command;
    }
}
