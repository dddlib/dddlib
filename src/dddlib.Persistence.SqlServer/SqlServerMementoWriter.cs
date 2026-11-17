using System.Data;
using System.Data.SqlTypes;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Transactions;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// One memento to write: the identity, memento and events of an aggregate root, and the repository that stores it.
/// </summary>
internal sealed record MementoWrite(Guid Id, object Memento, IReadOnlyList<object> Events, string? PreCommitState, ISqlServerBatchParticipant Participant);

/// <summary>
/// Writes mementos and their events in one transaction that dddlib owns: a <see cref="SqlServerBatch"/>, and a single
/// save of custom storage through the transactional hook of <see cref="SqlServerRepository{T}"/>. Every identity's lock
/// is taken first, in identity order; the mementos are written; the events are appended last, under the store-wide lock.
/// </summary>
internal static class SqlServerMementoWriter
{
    private static readonly SqlMetaData[] StreamIdColumns = [new("StreamId", SqlDbType.UniqueIdentifier)];

    private static readonly SqlMetaData[] MementoColumns =
    [
        new("Id", SqlDbType.UniqueIdentifier),
        new("Order", SqlDbType.Int),
        new("TypeId", SqlDbType.Int),
        new("Payload", SqlDbType.NVarChar, -1),
        new("PreCommitState", SqlDbType.VarChar, 36),
    ];

    private static readonly SqlMetaData[] StreamAppendColumns =
    [
        new("StreamId", SqlDbType.UniqueIdentifier),
        new("Order", SqlDbType.Int),
        new("State", SqlDbType.VarChar, 36),
        new("Metadata", SqlDbType.NVarChar, -1),
    ];

    // The transactions this writer owns, so that a hook cannot also append the events it is about to append.
    private static readonly ConditionalWeakTable<SqlTransaction, object> OwnedTransactions = [];

    /// <summary>
    /// Gets a value indicating whether dddlib owns the transaction, in which case it appends the events itself.
    /// </summary>
    public static bool Owns(SqlTransaction transaction) => OwnedTransactions.TryGetValue(transaction, out _);

    /// <summary>
    /// Writes the mementos and returns their new state tokens in order. A memento whose stored state does not match is
    /// a <see cref="StreamConcurrencyException"/> naming its identity, and nothing is written.
    /// </summary>
    public static async Task<string[]> WriteAsync(
        string connectionString,
        string quotedSchema,
        SqlServerTypeCache typeCache,
        IReadOnlyList<MementoWrite> writes,
        CancellationToken cancellationToken)
    {
        // Everything that needs the type registry or can fail to serialize happens before the transaction.
        var correlationId = Guid.NewGuid();
        var lockRecords = new List<SqlDataRecord>(writes.Count);
        var mementoRecords = new List<SqlDataRecord>();
        var appendRecords = new List<SqlDataRecord>();
        var appendIndexes = new List<int>();
        var eventRecords = new List<SqlDataRecord>();
        for (var order = 0; order < writes.Count; order++)
        {
            var write = writes[order];

            var lockRecord = new SqlDataRecord(StreamIdColumns);
            lockRecord.SetGuid(0, write.Id);
            lockRecords.Add(lockRecord);

            if (write.Participant.UsesMementoTable)
            {
                var record = new SqlDataRecord(MementoColumns);
                record.SetGuid(0, write.Id);
                record.SetInt32(1, order + 1);
                record.SetInt32(2, await typeCache.GetTypeIdAsync(write.Memento.GetType(), cancellationToken).ConfigureAwait(false));
                record.SetString(3, JsonSerializer.Serialize(write.Memento, write.Memento.GetType(), JsonSerialization.Options));
                SetNullableString(record, 4, write.PreCommitState);
                mementoRecords.Add(record);
            }

            if (write.Events.Count > 0)
            {
                // The state is set once the memento is written.
                var record = new SqlDataRecord(StreamAppendColumns);
                record.SetGuid(0, write.Id);
                record.SetInt32(1, order + 1);
                record.SetString(3, SqlServerEvents.CreateMetadata(write.Participant.EventMetadata));
                appendRecords.Add(record);
                appendIndexes.Add(order);
                await SqlServerEvents.AddStreamRecordsAsync(eventRecords, write.Id, write.Events, typeCache, cancellationToken).ConfigureAwait(false);
            }
        }

        var states = new string[writes.Count];

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(connectionString);

        await SqlServerSchemaCheck.EnsureCompatibleAsync(connectionString, quotedSchema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        OwnedTransactions.Add(transaction, transaction);

        try
        {
            await LockAsync(transaction, quotedSchema, lockRecords, cancellationToken).ConfigureAwait(false);

            if (mementoRecords.Count > 0)
            {
                await SaveMementosAsync(transaction, quotedSchema, mementoRecords, writes, states, cancellationToken).ConfigureAwait(false);
            }

            // Custom storage, one aggregate root at a time, in the identity order of the locks.
            foreach (var index in Enumerable.Range(0, writes.Count).Where(index => !writes[index].Participant.UsesMementoTable).OrderBy(index => new SqlGuid(writes[index].Id)))
            {
                states[index] = await SaveCustomAsync(transaction, writes[index], cancellationToken).ConfigureAwait(false);
            }

            if (appendRecords.Count > 0)
            {
                for (var index = 0; index < appendRecords.Count; index++)
                {
                    appendRecords[index].SetString(2, states[appendIndexes[index]]);
                }

                await AppendAsync(transaction, quotedSchema, appendRecords, eventRecords, correlationId, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Has(SqlServerErrors.LockTimeout) || ex.Has(SqlServerErrors.LockRequestTimeout))
        {
            throw new ConcurrencyException(ex.Message, ex);
        }
        finally
        {
            OwnedTransactions.Remove(transaction);
        }

        return states;
    }

    private static void SetNullableString(SqlDataRecord record, int ordinal, string? value)
    {
        if (value is null)
        {
            record.SetDBNull(ordinal);
        }
        else
        {
            record.SetString(ordinal, value);
        }
    }

    private static SqlCommand CreateCommand(SqlTransaction transaction, string quotedSchema, string procedure)
    {
        var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(quotedSchema, ".[", procedure, "]");
        return command;
    }

    private static SqlParameter AddTable(SqlCommand command, string name, string quotedSchema, string typeName, List<SqlDataRecord> records)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Structured);
        parameter.TypeName = string.Concat(quotedSchema, ".[", typeName, "]");
        parameter.Value = records;
        return parameter;
    }

    private static async Task LockAsync(SqlTransaction transaction, string quotedSchema, List<SqlDataRecord> records, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(transaction, quotedSchema, "LockStreams");
        AddTable(command, "@StreamIds", quotedSchema, "StreamIdList", records);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task SaveMementosAsync(SqlTransaction transaction, string quotedSchema, List<SqlDataRecord> records, IReadOnlyList<MementoWrite> writes, string[] states, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(transaction, quotedSchema, "SaveMementos");
        AddTable(command, "@Mementos", quotedSchema, "MementoList", records);
        var conflictParameter = command.Parameters.Add("@ConflictId", SqlDbType.UniqueIdentifier);
        conflictParameter.Direction = ParameterDirection.Output;

        var saved = new Dictionary<Guid, string>(records.Count);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                saved.Add(reader.GetGuid(0), reader.GetString(1));
            }
        }

        if (conflictParameter.Value is Guid conflictId)
        {
            var conflict = writes.First(write => write.Id == conflictId);
            throw new StreamConcurrencyException(
                conflictId,
                conflict.PreCommitState is null ? "Aggregate root already exists." : "Concurrency error (client side). Commit state mismatch.");
        }

        for (var index = 0; index < writes.Count; index++)
        {
            if (saved.TryGetValue(writes[index].Id, out var state))
            {
                states[index] = state;
            }
        }
    }

    private static async Task<string> SaveCustomAsync(SqlTransaction transaction, MementoWrite write, CancellationToken cancellationToken)
    {
        string state;
        try
        {
            state = await write.Participant.SaveMementoAsync(transaction, write.Id, write.Memento, write.PreCommitState, cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Has(SqlServerErrors.CommitStateMismatch))
        {
            throw new StreamConcurrencyException(write.Id, ex.Message, ex);
        }
        catch (ConcurrencyException ex) when (ex is not StreamConcurrencyException)
        {
            throw new StreamConcurrencyException(write.Id, ex.Message, ex);
        }

        if (string.IsNullOrEmpty(state))
        {
            throw new InvalidOperationException("The transactional 'SaveAsync' of a SQL Server repository must return the new state token of the memento.");
        }

        return state;
    }

    private static async Task AppendAsync(SqlTransaction transaction, string quotedSchema, List<SqlDataRecord> appendRecords, List<SqlDataRecord> eventRecords, Guid correlationId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(transaction, quotedSchema, "AppendStreams");
        AddTable(command, "@Streams", quotedSchema, "StreamAppendList", appendRecords);
        AddTable(command, "@Events", quotedSchema, "StreamEventList", eventRecords);
        command.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = correlationId;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
