using System.Data;
using System.Text.Json;
using System.Transactions;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// An event store backed by SQL Server. Events are written in one round trip through a table-valued parameter.
/// </summary>
public sealed class SqlServerEventStore : IEventStore
{
    private readonly SqlServerTypeCache typeCache;
    private readonly string connectionString;
    private readonly string schema;

    public SqlServerEventStore(string connectionString, string schema = "dbo")
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        this.connectionString = connectionString;
        this.schema = SqlServerIdentifier.Quote(schema);
        this.typeCache = new SqlServerTypeCache(connectionString, schema);
    }

    public async Task<StreamResult> GetStreamAsync(Guid streamId, int streamRevision, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(streamRevision);

        var rows = new List<(int TypeId, string Payload)>();
        string? state;

        using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
        await using (var connection = new SqlConnection(this.connectionString))
        await using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = string.Concat(this.schema, ".[GetStream]");
            command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;
            command.Parameters.Add("@StreamRevision", SqlDbType.Int).Value = streamRevision;
            var stateParameter = command.Parameters.Add("@State", SqlDbType.VarChar, 36);
            stateParameter.Direction = ParameterDirection.Output;

            await SqlServerSchemaCheck.EnsureCurrentAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add((reader.GetInt32(1), reader.GetString(2)));
                    }
                }

                state = stateParameter.Value is string value ? value : null;
            }
            catch (SqlException ex) when (ex.Has(SqlServerErrors.LockTimeout) || ex.Has(SqlServerErrors.LockRequestTimeout))
            {
                throw new ConcurrencyException(ex.Message, ex);
            }
        }

        var events = new object[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var payloadType = await this.typeCache.GetTypeAsync(rows[index].TypeId, cancellationToken).ConfigureAwait(false);
            events[index] = JsonSerializer.Deserialize(rows[index].Payload, payloadType, JsonSerialization.Options)!;
        }

        return new StreamResult(events, state);
    }

    public async Task<string> CommitStreamAsync(Guid streamId, IReadOnlyList<object> events, Guid correlationId, string? preCommitState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            throw new ArgumentException("At least one event is required.", nameof(events));
        }

        var records = await SqlServerEvents.ToRecordsAsync(events, this.typeCache, cancellationToken).ConfigureAwait(false);

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[CommitStream]");
        command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = streamId;
        SqlServerEvents.AddEventsParameter(command, this.schema, records);
        command.Parameters.Add("@Metadata", SqlDbType.NVarChar, -1).Value = SqlServerEvents.CreateMetadata();
        command.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = correlationId;
        command.Parameters.Add("@PreCommitState", SqlDbType.VarChar, 36).Value = (object?)preCommitState ?? DBNull.Value;
        var postCommitStateParameter = command.Parameters.Add("@PostCommitState", SqlDbType.VarChar, 36);
        postCommitStateParameter.Direction = ParameterDirection.Output;

        await SqlServerSchemaCheck.EnsureCurrentAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Has(SqlServerErrors.LockTimeout) || ex.Has(SqlServerErrors.LockRequestTimeout))
        {
            throw new ConcurrencyException(ex.Message, ex);
        }
        catch (SqlException ex) when (ex.Has(SqlServerErrors.CommitStateMismatch))
        {
            throw preCommitState is null
                ? new ConcurrencyException("Aggregate root already exists.", ex)
                : new ConcurrencyException(ex.Message, ex);
        }

        return (string)postCommitStateParameter.Value;
    }
}
