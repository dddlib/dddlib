using System.Data;
using dddlib.Persistence.Sdk;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// A memento repository with the identity map on SQL Server and custom storage supplied by the derived class,
/// for example a table shaped for the aggregate root. <see cref="AppendEventsAsync"/> appends the events of a save
/// to the aggregate root's stream inside the derived class's transaction, so that they can be dispatched.
/// </summary>
public abstract class SqlServerRepository<T> : Repository<T>
    where T : AggregateRoot
{
    private readonly SqlServerTypeCache typeCache;
    private readonly string schema;

    protected SqlServerRepository(string connectionString, string schema = "dbo")
        : base(new SqlServerIdentityMap(connectionString, schema))
    {
        this.ConnectionString = connectionString;
        this.schema = SqlServerIdentifier.Quote(schema);
        this.typeCache = new SqlServerTypeCache(connectionString, schema);
    }

    protected string ConnectionString { get; }

    /// <summary>
    /// Appends the events to the stream of the aggregate root identified by <paramref name="id"/>, in the caller's
    /// transaction, and sets the stream's state token to <paramref name="state"/>, the token the memento is being
    /// saved with. Call it after writing the memento and before committing, so that a dispatcher never sees events
    /// for a memento that was not saved. Does nothing when there are no events.
    /// </summary>
    protected async Task AppendEventsAsync(SqlTransaction transaction, Guid id, IReadOnlyList<object> events, string state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentException.ThrowIfNullOrEmpty(state);

        if (events.Count == 0)
        {
            return;
        }

        var connection = transaction.Connection ?? throw new ArgumentException("The transaction has already completed.", nameof(transaction));
        await SqlServerSchemaCheck.EnsureCurrentAsync(this.ConnectionString, this.schema, cancellationToken).ConfigureAwait(false);
        var records = await SqlServerEvents.ToRecordsAsync(events, this.typeCache, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[AppendEvents]");
        command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = id;
        SqlServerEvents.AddEventsParameter(command, this.schema, records);
        command.Parameters.Add("@State", SqlDbType.VarChar, 36).Value = state;
        command.Parameters.Add("@Metadata", SqlDbType.NVarChar, -1).Value = SqlServerEvents.CreateMetadata();
        command.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Has(SqlServerErrors.LockTimeout) || ex.Has(SqlServerErrors.LockRequestTimeout))
        {
            throw new ConcurrencyException(ex.Message, ex);
        }
    }
}
