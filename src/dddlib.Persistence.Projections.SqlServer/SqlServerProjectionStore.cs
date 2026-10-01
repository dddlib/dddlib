using System.Transactions;
using dddlib.Persistence.Projections.Sdk;
using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.Projections.SqlServer;

/// <summary>
/// Runs a key/value projection against SQL Server: handlers write to a buffer over the projection's
/// <see cref="SqlServerRepository{TIdentity, TEntity}"/>, and each page is committed in one transaction that moves the
/// checkpoint first and then saves the buffered views, so a page is applied exactly once however the runner fails.
/// The read model may be in a different database from the event store; both need the schema at version 2.
/// </summary>
/// <typeparam name="TIdentity">The type of the identity a view is keyed by.</typeparam>
/// <typeparam name="TEntity">The type of the view.</typeparam>
public sealed class SqlServerProjectionStore<TIdentity, TEntity> : IProjectionStore
    where TIdentity : notnull
    where TEntity : class
{
    private readonly Projection<TIdentity, TEntity> projection;
    private readonly string connectionString;
    private readonly string schema;

    public SqlServerProjectionStore(string connectionString, Projection<TIdentity, TEntity> projection, string schema = "dbo")
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentNullException.ThrowIfNull(projection);

        this.connectionString = connectionString;
        this.projection = projection;
        this.schema = SqlServerIdentifier.Quote(schema);
        this.Views = new SqlServerRepository<TIdentity, TEntity>(connectionString, projection.Name, schema);
    }

    public string Name => this.projection.Name;

    public IReadOnlyCollection<Type>? EventTypes => this.projection.EventTypes;

    /// <summary>
    /// Gets the views the projection writes, for readers in the same process. Readers elsewhere construct a
    /// <see cref="SqlServerRepository{TIdentity, TEntity}"/> with the same connection string and projection name.
    /// </summary>
    public SqlServerRepository<TIdentity, TEntity> Views { get; }

    public async Task<long> GetCheckpointAsync(CancellationToken cancellationToken = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await SqlServerProjectionCommands.GetCheckpointAsync(connection, null, this.schema, this.Name, cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyAsync(EventPage page, long expectedCheckpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        var buffer = new BufferedRepository<TIdentity, TEntity>(this.Views);
        foreach (var @event in page.Events)
        {
            await this.projection.ApplyAsync(@event, buffer, cancellationToken).ConfigureAwait(false);
        }

        var records = SqlServerProjectionCommands.ToRecords(buffer.Changes);

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await SqlServerProjectionCommands.AdvanceCheckpointAsync(connection, transaction, this.schema, this.Name, expectedCheckpoint, page.EndSequenceNumber, cancellationToken).ConfigureAwait(false);
        if (buffer.IsPurged)
        {
            await SqlServerProjectionCommands.DeleteViewsAsync(connection, transaction, this.schema, this.Name, cancellationToken).ConfigureAwait(false);
        }

        await SqlServerProjectionCommands.SaveViewsAsync(connection, transaction, this.schema, this.Name, records, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await SqlServerProjectionCommands.ResetCheckpointAsync(connection, transaction, this.schema, this.Name, cancellationToken).ConfigureAwait(false);
        await SqlServerProjectionCommands.DeleteViewsAsync(connection, transaction, this.schema, this.Name, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);

        var connection = new SqlConnection(this.connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
