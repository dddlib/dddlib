using System.Transactions;
using dddlib.Persistence.Projections.Sdk;
using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.Projections.SqlServer;

/// <summary>
/// A projection into your own tables. Handlers receive the <see cref="SqlTransaction"/> to write with; dddlib owns
/// the connection, the transaction and the checkpoint, which it moves first in the same transaction, so a page is
/// applied to your tables exactly once. Subclass it, register the handlers in the constructor and implement
/// <see cref="PurgeAsync(SqlTransaction, CancellationToken)"/> to clear your tables for a rebuild; creating and
/// migrating them is yours. The checkpoint lives in dddlib's schema in the same database, which must be at version 2.
/// <code>
/// public sealed class CarTableProjection : SqlServerProjection
/// {
///     public CarTableProjection(string connectionString)
///         : base(connectionString, "car-table")
///     {
///         this.When&lt;CarRegistered&gt;(async (e, transaction, cancellationToken) =>
///         {
///             await using var command = transaction.Connection!.CreateCommand();
///             command.Transaction = transaction;
///             command.CommandText = "INSERT INTO [dbo].[Cars] ([Registration]) VALUES (@Registration);";
///             command.Parameters.AddWithValue("@Registration", e.Registration);
///             await command.ExecuteNonQueryAsync(cancellationToken);
///         });
///     }
///
///     protected override async Task PurgeAsync(SqlTransaction transaction, CancellationToken cancellationToken)
///     {
///         // DELETE FROM [dbo].[Cars]
///     }
/// }
/// </code>
/// </summary>
public abstract class SqlServerProjection : ProjectionBase<SqlTransaction>, IProjectionStore
{
    private readonly string schema;

    protected SqlServerProjection(string connectionString, string name, string schema = "dbo")
        : base(name)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        this.ConnectionString = connectionString;
        this.schema = SqlServerIdentifier.Quote(schema);
    }

    protected string ConnectionString { get; }

    public async Task<long> GetCheckpointAsync(CancellationToken cancellationToken = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await SqlServerProjectionCommands.GetCheckpointAsync(connection, null, this.schema, this.Name, cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyAsync(EventPage page, long expectedCheckpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await SqlServerProjectionCommands.AdvanceCheckpointAsync(connection, transaction, this.schema, this.Name, expectedCheckpoint, page.EndSequenceNumber, cancellationToken).ConfigureAwait(false);
        foreach (var @event in page.Events)
        {
            await this.ApplyAsync(@event, transaction, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await SqlServerProjectionCommands.ResetCheckpointAsync(connection, transaction, this.schema, this.Name, cancellationToken).ConfigureAwait(false);
        await this.PurgeAsync(transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears your tables in the given transaction, for a rebuild. The checkpoint has already been reset in it.
    /// </summary>
    protected abstract Task PurgeAsync(SqlTransaction transaction, CancellationToken cancellationToken);

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.ConnectionString, this.schema, cancellationToken).ConfigureAwait(false);

        var connection = new SqlConnection(this.ConnectionString);
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
