using System.Data;
using System.Runtime.CompilerServices;
using System.Transactions;
using dddlib.Persistence.SqlServer;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.Projections.SqlServer;

/// <summary>
/// The views of a key/value projection, stored as JSON in the <c>ProjectionViews</c> table under the projection's
/// name, for readers and for the <see cref="SqlServerProjectionStore{TIdentity, TEntity}"/> that writes them. Keys
/// are the identity serialized as JSON (so a string key carries its quotes), compared ordinally, and at most 400
/// characters long. The schema must be at version 2 or later; see <see cref="SqlServerProjectionsSchema"/>.
/// </summary>
/// <typeparam name="TIdentity">The type of the identity a view is keyed by.</typeparam>
/// <typeparam name="TEntity">The type of the view.</typeparam>
public sealed class SqlServerRepository<TIdentity, TEntity> : IRepository<TIdentity, TEntity>
    where TIdentity : notnull
    where TEntity : class
{
    private readonly string connectionString;
    private readonly string schema;

    public SqlServerRepository(string connectionString, string projectionName, string schema = "dbo")
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);

        this.connectionString = connectionString;
        this.ProjectionName = projectionName;
        this.schema = SqlServerIdentifier.Quote(schema);
    }

    /// <summary>
    /// Gets the name of the projection whose views these are.
    /// </summary>
    public string ProjectionName { get; }

    public async Task<TEntity?> GetAsync(TIdentity identity, CancellationToken cancellationToken = default)
    {
        var key = SqlServerProjectionCommands.SerializeKey(identity);

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = SqlServerProjectionCommands.Create(connection, null, this.schema, "GetProjectionView", this.ProjectionName);
        command.Parameters.Add("@Key", SqlDbType.NVarChar, SqlServerProjectionCommands.MaxKeyLength).Value = key;

        var payload = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return payload is string json ? SqlServerProjectionCommands.DeserializeView<TEntity>(json) : null;
    }

    public async IAsyncEnumerable<KeyValuePair<TIdentity, TEntity>> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = SqlServerProjectionCommands.Create(connection, null, this.schema, "GetProjectionViews", this.ProjectionName);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new KeyValuePair<TIdentity, TEntity>(
                SqlServerProjectionCommands.DeserializeKey<TIdentity>(reader.GetString(0)),
                SqlServerProjectionCommands.DeserializeView<TEntity>(reader.GetString(1)));
        }
    }

    public Task AddOrUpdateAsync(TIdentity identity, TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return this.SaveAsync([new KeyValuePair<TIdentity, TEntity?>(identity, entity)], cancellationToken);
    }

    public Task RemoveAsync(TIdentity identity, CancellationToken cancellationToken = default) =>
        this.SaveAsync([new KeyValuePair<TIdentity, TEntity?>(identity, null)], cancellationToken);

    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await SqlServerProjectionCommands.DeleteViewsAsync(connection, null, this.schema, this.ProjectionName, cancellationToken).ConfigureAwait(false);
    }

    public Task BulkUpdateAsync(IEnumerable<KeyValuePair<TIdentity, TEntity>> addOrUpdate, IEnumerable<TIdentity> remove, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addOrUpdate);
        ArgumentNullException.ThrowIfNull(remove);

        // The last change per identity wins, as it would applied one by one.
        var changes = new Dictionary<TIdentity, TEntity?>();
        foreach (var item in addOrUpdate)
        {
            ArgumentNullException.ThrowIfNull(item.Value, nameof(addOrUpdate));
            changes[item.Key] = item.Value;
        }

        foreach (var identity in remove)
        {
            changes[identity] = null;
        }

        return this.SaveAsync(changes, cancellationToken);
    }

    private async Task SaveAsync(IEnumerable<KeyValuePair<TIdentity, TEntity?>> changes, CancellationToken cancellationToken)
    {
        var records = SqlServerProjectionCommands.ToRecords(changes);

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await SqlServerProjectionCommands.SaveViewsAsync(connection, null, this.schema, this.ProjectionName, records, cancellationToken).ConfigureAwait(false);
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
