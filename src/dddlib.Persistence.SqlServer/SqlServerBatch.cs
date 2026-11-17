using System.Globalization;
using dddlib.Persistence.Sdk;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// A batch of aggregate roots saved in one SQL Server transaction through <see cref="SqlServerMementoRepository{T}"/>
/// and <see cref="SqlServerRepository{T}"/> instances, of any aggregate root types, over the same database and schema:
/// every memento is stored and every event appended, or nothing is. Custom storage takes part through the transactional
/// <c>SaveAsync</c> of <see cref="SqlServerRepository{T}"/>, in the batch's transaction.
/// </summary>
public sealed class SqlServerBatch : IBatch
{
    private readonly BatchSaves<ISqlServerBatchParticipant> saves = new();
    private readonly SqlServerTypeCache typeCache;
    private readonly string connectionString;
    private readonly string schema;

    public SqlServerBatch(string connectionString, string schema = "dbo")
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        this.connectionString = connectionString;
        this.schema = SqlServerIdentifier.Quote(schema);
        this.typeCache = new SqlServerTypeCache(connectionString, schema);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var entries = this.saves.Start();

        await BatchSaves<ISqlServerBatchParticipant>.SaveAsync(entries, WriteAsync, cancellationToken).ConfigureAwait(false);

        async Task<IReadOnlyList<string>> WriteAsync(IReadOnlyList<(BatchSave Save, ISqlServerBatchParticipant Participant)> batch)
        {
            var writes = batch.Select(static entry => new MementoWrite(entry.Save.Id, entry.Save.Memento, entry.Save.Events, entry.Save.PreCommitState, entry.Participant)).ToArray();
            try
            {
                return await SqlServerMementoWriter.WriteAsync(this.connectionString, this.schema, this.typeCache, writes, cancellationToken).ConfigureAwait(false);
            }
            catch (StreamConcurrencyException ex)
            {
                var save = batch.First(entry => entry.Save.Id == ex.StreamId).Save;
                throw save.Conflict(save.PreCommitState is null ? "already exists" : "has changed since it was loaded", ex);
            }
        }
    }

    /// <summary>
    /// Adds a save through a repository over the same database and schema; another database or schema is an
    /// <see cref="ArgumentException"/>, since the batch writes everything on its own connection.
    /// </summary>
    internal void Add(BatchSave save, ISqlServerBatchParticipant participant)
    {
        if (!this.IsSameDatabase(participant))
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The repository writes schema {0} of database '{1}', but the batch writes schema {2} of database '{3}'. A batch saves in one transaction, so its repositories must use its database and schema.",
                    participant.Schema,
                    Describe(participant.ConnectionString),
                    this.schema,
                    Describe(this.connectionString)));
        }

        this.saves.Add(save, participant);
    }

    private static string Describe(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return string.Concat(builder.DataSource, "/", builder.InitialCatalog);
    }

    private bool IsSameDatabase(ISqlServerBatchParticipant participant)
    {
        if (!string.Equals(this.schema, participant.Schema, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var batch = new SqlConnectionStringBuilder(this.connectionString);
        var repository = new SqlConnectionStringBuilder(participant.ConnectionString);

        return string.Equals(batch.DataSource, repository.DataSource, StringComparison.OrdinalIgnoreCase)
            && string.Equals(batch.InitialCatalog, repository.InitialCatalog, StringComparison.OrdinalIgnoreCase);
    }
}
