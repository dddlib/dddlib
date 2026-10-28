using System.Collections.ObjectModel;
using System.Data;
using System.Text.Json;
using System.Transactions;
using dddlib.Persistence.EventDispatcher.Sdk;
using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
using dddlib.Sdk;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.EventDispatcher.SqlServer;

/// <summary>
/// Batches the events in the SQL Server event store. Several dispatcher instances with the same dispatcher id may
/// run at once; each poll takes an application lock on the dispatcher id, and a poll that cannot get it in time
/// simply finds no batch.
/// </summary>
public sealed class SqlServerEventBatchStore : IEventBatchStore
{
    private readonly string connectionString;
    private readonly string schema;

    public SqlServerEventBatchStore(string connectionString, string schema = "dbo")
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        this.connectionString = connectionString;
        this.schema = SqlServerIdentifier.Quote(schema);
    }

    public async Task<EventBatch?> GetNextBatchAsync(Guid dispatcherId, int batchSize, TimeSpan batchTimeout, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        long? batchId = null;
        var rows = new List<(long SequenceNumber, string TypeName, string Payload, string? Metadata)>();

        using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
        await using (var connection = new SqlConnection(this.connectionString))
        await using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = string.Concat(this.schema, ".[GetNextBatch]");
            command.Parameters.Add("@DispatcherId", SqlDbType.UniqueIdentifier).Value = dispatcherId;
            command.Parameters.Add("@MaxBatchSize", SqlDbType.Int).Value = batchSize;
            command.Parameters.Add("@BatchTimeoutMilliseconds", SqlDbType.Int).Value = (int)Math.Clamp(Math.Ceiling(batchTimeout.TotalMilliseconds), 0, int.MaxValue);

            await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    batchId = reader.GetInt64(0);
                }

                await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
                }
            }
            catch (SqlException ex) when (ex.Has(SqlServerErrors.LockTimeout) || ex.Has(SqlServerErrors.LockRequestTimeout))
            {
                return null;
            }
        }

        if (batchId is null)
        {
            return null;
        }

        var events = new DispatchedEvent[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var payloadType = TypeNameResolver.ResolveOrThrow(rows[index].TypeName);
            events[index] = new DispatchedEvent(
                rows[index].SequenceNumber,
                ReadMetadata(rows[index].Metadata),
                JsonSerializer.Deserialize(rows[index].Payload, payloadType, JsonSerialization.Options)!);
        }

        return new EventBatch(batchId.Value, events);
    }

    public async Task MarkDispatchedAsync(Guid dispatcherId, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[MarkDispatched]");
        command.Parameters.Add("@DispatcherId", SqlDbType.UniqueIdentifier).Value = dispatcherId;
        command.Parameters.Add("@SequenceNumber", SqlDbType.BigInt).Value = sequenceNumber;

        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // The string entries of the stored JSON object. Anything else is absent rather than an error, which would fail the
    // batch each time it is handed out again.
    private static ReadOnlyDictionary<string, string> ReadMetadata(string? json)
    {
        if (json is null)
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ReadOnlyDictionary<string, string>.Empty;
            }

            var metadata = new Dictionary<string, string>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    metadata[property.Name] = property.Value.GetString()!;
                }
            }

            return metadata.AsReadOnly();
        }
        catch (JsonException)
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }
    }
}
