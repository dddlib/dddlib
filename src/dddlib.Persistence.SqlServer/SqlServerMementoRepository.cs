using System.Data;
using System.Text.Json;
using System.Transactions;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// A memento repository backed by the <c>Mementos</c> table on SQL Server. The events of each save are appended to
/// the aggregate root's stream in the same transaction, so that they can be dispatched. The schema must have been
/// created with the shipped scripts.
/// </summary>
public sealed class SqlServerMementoRepository<T> : Repository<T>
    where T : AggregateRoot
{
    private readonly SqlServerTypeCache typeCache;
    private readonly string connectionString;
    private readonly string schema;

    public SqlServerMementoRepository(string connectionString, string schema = "dbo")
        : base(new SqlServerIdentityMap(connectionString, schema))
    {
        this.connectionString = connectionString;
        this.schema = SqlServerIdentifier.Quote(schema);
        this.typeCache = new SqlServerTypeCache(connectionString, schema);
    }

    protected override async Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        int typeId;
        string payload;
        string state;

        using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
        await using (var connection = new SqlConnection(this.connectionString))
        await using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = string.Concat(this.schema, ".[LoadMemento]");
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;

            await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            typeId = reader.GetInt32(1);
            payload = reader.GetString(2);
            state = reader.GetString(3);
        }

        var payloadType = await this.typeCache.GetTypeAsync(typeId, cancellationToken).ConfigureAwait(false);
        var memento = JsonSerializer.Deserialize(payload, payloadType, JsonSerialization.Options)!;

        return new MementoResult(memento, state);
    }

    protected override async Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, string? preCommitState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memento);
        ArgumentNullException.ThrowIfNull(events);

        var typeId = await this.typeCache.GetTypeIdAsync(memento.GetType(), cancellationToken).ConfigureAwait(false);
        var records = await SqlServerEvents.ToRecordsAsync(events, this.typeCache, cancellationToken).ConfigureAwait(false);

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[SaveMemento]");
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        command.Parameters.Add("@TypeId", SqlDbType.Int).Value = typeId;
        command.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(memento, memento.GetType(), JsonSerialization.Options);
        command.Parameters.Add("@PreCommitState", SqlDbType.VarChar, 36).Value = (object?)preCommitState ?? DBNull.Value;
        var postCommitStateParameter = command.Parameters.Add("@PostCommitState", SqlDbType.VarChar, 36);
        postCommitStateParameter.Direction = ParameterDirection.Output;

        // The procedure appends the events to the aggregate root's stream in the memento's transaction. An omitted
        // table-valued parameter is an empty table.
        if (records.Count > 0)
        {
            SqlServerEvents.AddEventsParameter(command, this.schema, records);
            command.Parameters.Add("@Metadata", SqlDbType.NVarChar, -1).Value = SqlServerEvents.CreateMetadata();
            command.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        }

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
}
