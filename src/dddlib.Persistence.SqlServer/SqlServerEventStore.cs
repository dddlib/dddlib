using System.Data;
using System.Text.Json;
using System.Transactions;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// An event store backed by SQL Server. Events are written in one round trip through a table-valued parameter. It is
/// also the <see cref="IEventFeed"/> for projections over it, which may run against another database.
/// </summary>
public sealed class SqlServerEventStore : IEventStore, IEventFeed
{
    private static readonly SqlMetaData[] TypeNameColumns = [new("Name", SqlDbType.VarChar, 511)];

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

            await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
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

        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
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

    public async Task<EventPage> ReadEventsAsync(long afterSequenceNumber, int maxCount, IReadOnlyCollection<Type>? eventTypes = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequenceNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        long? endSequenceNumber = null;
        var rows = new List<(long SequenceNumber, Guid StreamId, int StreamRevision, Guid CorrelationId, string TypeName, string Payload)>();

        using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
        await using (var connection = new SqlConnection(this.connectionString))
        await using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = string.Concat(this.schema, ".[ReadEvents]");
            command.Parameters.Add("@AfterSequenceNumber", SqlDbType.BigInt).Value = afterSequenceNumber;
            command.Parameters.Add("@MaxCount", SqlDbType.Int).Value = maxCount;
            this.AddTypeNamesParameter(command, eventTypes);

            await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && !await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
            {
                endSequenceNumber = reader.GetInt64(0);
            }

            await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((reader.GetInt64(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetGuid(3), reader.GetString(4), reader.GetString(5)));
            }
        }

        var events = new FeedEvent[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var payloadType = TypeNameResolver.ResolveOrThrow(row.TypeName);
            var @event = JsonSerializer.Deserialize(row.Payload, payloadType, JsonSerialization.Options)!;
            events[index] = new FeedEvent(row.SequenceNumber, row.StreamId, row.StreamRevision, row.CorrelationId, @event);
        }

        return new EventPage(endSequenceNumber ?? afterSequenceNumber, events);
    }

    public async Task<long> GetLastSequenceNumberAsync(CancellationToken cancellationToken = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[GetLastSequenceNumber]");

        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    // An omitted table-valued parameter is an empty table, which the procedure reads as "every type"; SqlClient rejects
    // an empty enumeration, so the parameter is only added when there is something to filter by.
    private void AddTypeNamesParameter(SqlCommand command, IReadOnlyCollection<Type>? eventTypes)
    {
        if (eventTypes is null || eventTypes.Count == 0)
        {
            return;
        }

        var records = eventTypes
            .Select(static type => type.GetSerializedName())
            .Distinct(StringComparer.Ordinal)
            .Select(name =>
            {
                var record = new SqlDataRecord(TypeNameColumns);
                record.SetString(0, name);
                return record;
            })
            .ToList();

        var parameter = command.Parameters.Add("@TypeNames", SqlDbType.Structured);
        parameter.TypeName = string.Concat(this.schema, ".[TypeNameList]");
        parameter.Value = records;
    }
}
