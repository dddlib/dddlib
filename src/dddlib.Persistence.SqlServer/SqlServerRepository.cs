using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Reflection;
using dddlib.Persistence.Sdk;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// A memento repository with custom storage supplied by the derived class, for example a table shaped for the
/// aggregate root, and the identity map on SQL Server unless one is supplied.
/// <para>
/// Override the transactional <see cref="SaveAsync(SqlTransaction, Guid, object, string?, CancellationToken)"/> to write
/// the memento in a transaction dddlib owns: dddlib takes the identity's lock, appends the events with
/// <see cref="EventMetadata"/> and commits, and the repository can take part in a <see cref="SqlServerBatch"/>.
/// Alternatively override <see cref="SaveAsync(Guid, object, IReadOnlyList{object}, Guid, string?, CancellationToken)"/> and
/// own the transaction, calling <see cref="AppendEventsAsync(SqlTransaction, Guid, IReadOnlyList{object}, Guid, string, CancellationToken)"/>
/// so that the events can be dispatched; such a repository cannot take part in a batch.
/// </para>
/// </summary>
public abstract class SqlServerRepository<T> : Repository<T>, ISqlServerBatchParticipant
    where T : AggregateRoot
{
    private static readonly ConcurrentDictionary<Type, (bool Transactional, bool OwnsTransaction)> Hooks = new();

    private readonly SqlServerTypeCache typeCache;
    private readonly string schema;

    protected SqlServerRepository(string connectionString, string schema = "dbo")
        : this(connectionString, new SqlServerIdentityMap(connectionString, schema), schema)
    {
    }

    /// <summary>
    /// Creates the repository with an identity map other than <see cref="SqlServerIdentityMap"/>, for example one
    /// that reads the identity from the derived class's own table. <paramref name="schema"/> is still the dddlib
    /// schema that the events are appended to.
    /// </summary>
    protected SqlServerRepository(string connectionString, IIdentityMap identityMap, string schema = "dbo")
        : base(identityMap)
    {
        this.ConnectionString = connectionString;
        this.schema = SqlServerIdentifier.Quote(schema);
        this.typeCache = new SqlServerTypeCache(connectionString, schema);
    }

    protected string ConnectionString { get; }

    /// <summary>
    /// Gets metadata that the events do not carry, such as a tenant, stored beside dddlib's own <c>hostname</c> and
    /// <c>timestamp</c> entries and given to the event dispatcher with each event, for saves through the transactional
    /// <see cref="SaveAsync(SqlTransaction, Guid, object, string?, CancellationToken)"/>. A key equal to either of
    /// dddlib's is an <see cref="ArgumentException"/>, and nothing is written.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, string>? EventMetadata => null;

    string ISqlServerBatchParticipant.ConnectionString => this.ConnectionString;

    string ISqlServerBatchParticipant.Schema => this.schema;

    bool ISqlServerBatchParticipant.UsesMementoTable => false;

    IReadOnlyDictionary<string, string>? ISqlServerBatchParticipant.EventMetadata => this.EventMetadata;

    private (bool Transactional, bool OwnsTransaction) Hook => Hooks.GetOrAdd(this.GetType(), FindHooks);

    Task<string> ISqlServerBatchParticipant.SaveMementoAsync(SqlTransaction transaction, Guid id, object memento, string? preCommitState, CancellationToken cancellationToken) =>
        this.SaveAsync(transaction, id, memento, preCommitState, cancellationToken);

    IReadOnlyDictionary<string, string>? ISqlServerBatchParticipant.GetEventMetadata(Guid id, object @event, int index) =>
        this.GetEventMetadata(id, @event, index);

    internal override void AddToBatch(IBatch batch, BatchSave save)
    {
        if (batch is not SqlServerBatch sqlServerBatch)
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "A SQL Server repository saves aggregate roots only in a '{0}'.", typeof(SqlServerBatch)),
                nameof(batch));
        }

        var (transactional, ownsTransaction) = this.Hook;
        if (ownsTransaction || !transactional)
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The repository of type '{0}' cannot save aggregate roots in a batch as it {1}.
To fix this issue:
- override 'SaveAsync(SqlTransaction transaction, Guid id, object memento, string? preCommitState, CancellationToken cancellationToken)' to write only the memento in the given transaction and return its new state token, and
- remove the override of 'SaveAsync(Guid id, object memento, IReadOnlyList<object> events, Guid correlationId, string? preCommitState, CancellationToken cancellationToken)': dddlib then appends the events, with any 'EventMetadata', and commits.",
                    this.GetType(),
                    ownsTransaction ? "owns the transaction it saves in" : "does not override the transactional 'SaveAsync'"));
        }

        sqlServerBatch.Add(save, this);
    }

    /// <summary>
    /// Writes the memento in <paramref name="transaction"/>, which dddlib owns, and returns its new state token.
    /// <paramref name="preCommitState"/> is null for a new aggregate root; when the stored state does not match, raise
    /// SQL Server error 50409 or throw a <see cref="ConcurrencyException"/>. dddlib holds the identity's lock, appends the
    /// events with <see cref="EventMetadata"/> after this returns, and commits; do not commit, roll back or append
    /// events here. Override this, rather than <see cref="SaveAsync(Guid, object, IReadOnlyList{object}, Guid, string?, CancellationToken)"/>,
    /// for the repository to take part in a <see cref="SqlServerBatch"/>.
    /// </summary>
    protected virtual Task<string> SaveAsync(SqlTransaction transaction, Guid id, object memento, string? preCommitState, CancellationToken cancellationToken) =>
        throw this.NoHook();

    /// <summary>
    /// Gets metadata of one event's own, such as a message ID, for saves through the transactional
    /// <see cref="SaveAsync(SqlTransaction, Guid, object, string?, CancellationToken)"/>, alone or in a
    /// <see cref="SqlServerBatch"/>. It is called once for each event before anything is written, and its entries are
    /// merged over <see cref="EventMetadata"/>: an entry with the same key replaces that one for this event only.
    /// <paramref name="id"/> identifies the aggregate root, since a batch saves several, and <paramref name="index"/> is
    /// the event's position, from 0, among the aggregate root's uncommitted events. A key equal to dddlib's own
    /// <c>hostname</c> or <c>timestamp</c> is an <see cref="ArgumentException"/>, and nothing is written. Returns null by
    /// default, for no metadata of the event's own.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, string>? GetEventMetadata(Guid id, object @event, int index) => null;

    /// <summary>
    /// Saves through the transactional <see cref="SaveAsync(SqlTransaction, Guid, object, string?, CancellationToken)"/>
    /// in a transaction dddlib owns. A derived class that overrides this owns the transaction instead.
    /// </summary>
    protected override async Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, Guid correlationId, string? preCommitState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memento);
        ArgumentNullException.ThrowIfNull(events);

        if (!this.Hook.Transactional)
        {
            throw this.NoHook();
        }

        var states = await SqlServerMementoWriter.WriteAsync(
            this.ConnectionString,
            this.schema,
            this.typeCache,
            [new MementoWrite(id, memento, events, preCommitState, this)],
            correlationId,
            cancellationToken).ConfigureAwait(false);

        return states[0];
    }

    /// <summary>
    /// Appends the events to the stream of the aggregate root identified by <paramref name="id"/>, in the caller's
    /// transaction, and sets the stream's state token to <paramref name="state"/>, the token the memento is being
    /// saved with. Call it after writing the memento and before committing, so that a dispatcher never sees events
    /// for a memento that was not saved. Stores <paramref name="correlationId"/>, the one the save was given, with every
    /// event; an empty one is an <see cref="ArgumentException"/>. Does nothing when there are no events. Only for a repository that owns its
    /// transaction: in the transactional <see cref="SaveAsync(SqlTransaction, Guid, object, string?, CancellationToken)"/>
    /// dddlib appends the events, and calling this is an <see cref="InvalidOperationException"/>.
    /// </summary>
    protected Task AppendEventsAsync(SqlTransaction transaction, Guid id, IReadOnlyList<object> events, Guid correlationId, string state, CancellationToken cancellationToken) =>
        this.AppendAsync(transaction, id, null, events, null, correlationId, state, cancellationToken);

    /// <summary>
    /// Appends the events as <see cref="AppendEventsAsync(SqlTransaction, Guid, IReadOnlyList{object}, Guid, string, CancellationToken)"/>
    /// does, with <paramref name="metadata"/> that the events do not carry, such as a tenant, stored beside dddlib's
    /// own <c>hostname</c> and <c>timestamp</c> entries and given to the event dispatcher with each event. A key equal
    /// to either of dddlib's is an <see cref="ArgumentException"/>, and nothing is written.
    /// </summary>
    protected Task AppendEventsAsync(SqlTransaction transaction, Guid id, IReadOnlyDictionary<string, string> metadata, IReadOnlyList<object> events, Guid correlationId, string state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return this.AppendAsync(transaction, id, metadata, events, null, correlationId, state, cancellationToken);
    }

    /// <summary>
    /// Appends the events as <see cref="AppendEventsAsync(SqlTransaction, Guid, IReadOnlyDictionary{string, string}, IReadOnlyList{object}, Guid, string, CancellationToken)"/>
    /// does, with metadata of each event's own, such as a message ID, merged over <paramref name="metadata"/>: an entry
    /// with the same key replaces that one for its event only. <paramref name="eventMetadata"/> is in step with
    /// <paramref name="events"/>, and a null or empty entry gives its event only <paramref name="metadata"/>. A different
    /// number of entries than events, or a key equal to dddlib's own <c>hostname</c> or <c>timestamp</c> at either level,
    /// is an <see cref="ArgumentException"/>, and nothing is written.
    /// </summary>
    protected Task AppendEventsAsync(
        SqlTransaction transaction,
        Guid id,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyList<object> events,
        IReadOnlyList<IReadOnlyDictionary<string, string>?> eventMetadata,
        Guid correlationId,
        string state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(eventMetadata);

        return this.AppendAsync(transaction, id, metadata, events, eventMetadata, correlationId, state, cancellationToken);
    }

    private static (bool Transactional, bool OwnsTransaction) FindHooks(Type type)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        var transactional = type.GetMethod(nameof(SaveAsync), Flags, [typeof(SqlTransaction), typeof(Guid), typeof(object), typeof(string), typeof(CancellationToken)]);
        var owning = type.GetMethod(nameof(SaveAsync), Flags, [typeof(Guid), typeof(object), typeof(IReadOnlyList<object>), typeof(Guid), typeof(string), typeof(CancellationToken)]);

        return (transactional?.DeclaringType != typeof(SqlServerRepository<T>), owning?.DeclaringType != typeof(SqlServerRepository<T>));
    }

    private NotSupportedException NoHook() =>
        new(
            string.Format(
                CultureInfo.InvariantCulture,
                @"The repository of type '{0}' does not write mementos.
To fix this issue:
- override 'SaveAsync(SqlTransaction transaction, Guid id, object memento, string? preCommitState, CancellationToken cancellationToken)' to write the memento in the given transaction and return its new state token.",
                this.GetType()));

    private async Task AppendAsync(
        SqlTransaction transaction,
        Guid id,
        IReadOnlyDictionary<string, string>? metadata,
        IReadOnlyList<object> events,
        IReadOnlyList<IReadOnlyDictionary<string, string>?>? eventMetadata,
        Guid correlationId,
        string state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(events);
        CorrelationIds.Validate(correlationId, nameof(correlationId));
        ArgumentException.ThrowIfNullOrEmpty(state);

        if (SqlServerMementoWriter.Owns(transaction))
        {
            throw new InvalidOperationException(
                "The events of a save through the transactional 'SaveAsync' are appended by dddlib, with any 'EventMetadata'. Do not append them in that method.");
        }

        if (eventMetadata is not null && eventMetadata.Count != events.Count)
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "The event metadata has {0} entries for {1} events.", eventMetadata.Count, events.Count),
                nameof(eventMetadata));
        }

        // One timestamp for every event of the append.
        var timestamp = DateTime.UtcNow;
        var metadataJson = SqlServerEvents.CreateMetadata(metadata, null, timestamp);
        var eventMetadataRecords = new List<SqlDataRecord>();
        if (eventMetadata is not null)
        {
            SqlServerEvents.AddEventMetadataRecords(eventMetadataRecords, id, metadata, eventMetadata, timestamp);
        }

        if (events.Count == 0)
        {
            return;
        }

        var connection = transaction.Connection ?? throw new ArgumentException("The transaction has already completed.", nameof(transaction));
        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.ConnectionString, this.schema, cancellationToken).ConfigureAwait(false);
        var records = await SqlServerEvents.ToRecordsAsync(events, this.typeCache, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[AppendEvents]");
        command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = id;
        SqlServerEvents.AddEventsParameter(command, this.schema, records);
        command.Parameters.Add("@State", SqlDbType.VarChar, 36).Value = state;
        command.Parameters.Add("@Metadata", SqlDbType.NVarChar, -1).Value = metadataJson;
        command.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = correlationId;
        SqlServerEvents.AddEventMetadataParameter(command, this.schema, eventMetadataRecords);

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
