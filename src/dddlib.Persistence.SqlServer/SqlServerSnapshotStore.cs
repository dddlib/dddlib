using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Transactions;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

public sealed class SqlServerSnapshotStore : ISnapshotStore
{
    private readonly string connectionString;
    private readonly string schema;

    public SqlServerSnapshotStore(string connectionString, string schema = "dbo")
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        this.connectionString = connectionString;
        this.schema = SqlServerIdentifier.Quote(schema);
    }

    public async Task<Snapshot?> GetSnapshotAsync(Guid streamId, CancellationToken cancellationToken = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[GetSnapshot]");
        command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;

        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var streamRevision = reader.GetInt32(1);
        var payloadTypeName = reader.GetString(2);
        var payload = reader.GetString(3);

        var payloadType = TypeNameResolver.Resolve(payloadTypeName)
            ?? throw new PersistenceException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"Cannot deserialize snapshot into type of '{0}' as that type does not exist in the assembly '{1}' or the assembly is not referenced by the project.
To fix this issue:
- ensure that the assembly '{1}' contains the type '{0}', and
- check that the assembly '{1}' is referenced by the project.
Further information: https://github.com/dddlib/dddlib/blob/main/docs/persistence/serialization.md",
                    payloadTypeName.Split(',').First().Trim(),
                    payloadTypeName.Split(',').Last().Trim()));

        return new Snapshot(streamRevision, JsonSerializer.Deserialize(payload, payloadType, JsonSerialization.Options));
    }

    public async Task PutSnapshotAsync(Guid streamId, Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Memento);

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[PutSnapshot]");
        command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;
        command.Parameters.Add("@StreamRevision", SqlDbType.Int).Value = snapshot.StreamRevision;
        command.Parameters.Add("@PayloadTypeName", SqlDbType.VarChar, 511).Value = snapshot.Memento.GetType().GetSerializedName();
        command.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value =
            JsonSerializer.Serialize(snapshot.Memento, snapshot.Memento.GetType(), JsonSerialization.Options);

        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
