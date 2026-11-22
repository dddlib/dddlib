using System.Data;
using System.Transactions;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

public sealed class SqlServerNaturalKeyRepository : IUniqueNaturalKeyRepository
{
    private readonly string connectionString;
    private readonly string schema;

    public SqlServerNaturalKeyRepository(string connectionString, string schema = "dbo")
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        this.connectionString = connectionString;
        this.schema = SqlServerIdentifier.Quote(schema);
    }

    public async Task<IReadOnlyList<NaturalKeyRecord>> GetNaturalKeysAsync(Type aggregateRootType, long checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[GetNaturalKeys]");
        command.Parameters.Add("@AggregateRootTypeName", SqlDbType.VarChar, 511).Value = aggregateRootType.GetSerializedName();
        command.Parameters.Add("@Checkpoint", SqlDbType.BigInt).Value = checkpoint;

        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var records = new List<NaturalKeyRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            records.Add(new NaturalKeyRecord(reader.GetGuid(0), reader.GetString(1), reader.GetInt64(2), reader.GetBoolean(3)));
        }

        return records;
    }

    public async Task<NaturalKeyRecord?> TryAddNaturalKeyAsync(Type aggregateRootType, string serializedNaturalKey, long checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);
        ArgumentNullException.ThrowIfNull(serializedNaturalKey);

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[TryAddNaturalKey]");
        command.Parameters.Add("@AggregateRootTypeName", SqlDbType.VarChar, 511).Value = aggregateRootType.GetSerializedName();
        command.Parameters.Add("@SerializedValue", SqlDbType.NVarChar, -1).Value = serializedNaturalKey;
        command.Parameters.Add("@Checkpoint", SqlDbType.BigInt).Value = checkpoint;

        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return new NaturalKeyRecord(reader.GetGuid(0), serializedNaturalKey, reader.GetInt64(1), IsRemoved: false);
        }
        catch (SqlException ex) when (ex.Has(SqlServerErrors.UniqueIndexViolation) || ex.Has(SqlServerErrors.Deadlock))
        {
            // Another caller added the key first, or this one deadlocked; the caller synchronizes and retries.
            return null;
        }
        catch (SqlException ex) when (IsLockTimeout(ex))
        {
            throw new ConcurrencyException(ex.Message, ex);
        }
    }

    public async Task<NaturalKeyRecord> GetOrAddNaturalKeyAsync(Type aggregateRootType, string serializedNaturalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);
        ArgumentNullException.ThrowIfNull(serializedNaturalKey);

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[GetOrAddNaturalKey]");
        command.Parameters.Add("@AggregateRootTypeName", SqlDbType.VarChar, 511).Value = aggregateRootType.GetSerializedName();
        command.Parameters.Add("@SerializedValue", SqlDbType.NVarChar, -1).Value = serializedNaturalKey;

        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new PersistenceException("The natural key was neither found nor added.");
            }

            return new NaturalKeyRecord(reader.GetGuid(0), serializedNaturalKey, reader.GetInt64(1), IsRemoved: false);
        }
        catch (SqlException ex) when (IsLockTimeout(ex))
        {
            throw new ConcurrencyException(ex.Message, ex);
        }
    }

    public async Task RemoveAsync(Guid naturalKeyIdentity, CancellationToken cancellationToken = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[RemoveNaturalKey]");
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = naturalKeyIdentity;

        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (IsLockTimeout(ex))
        {
            throw new ConcurrencyException(ex.Message, ex);
        }
    }

    // The type's checkpoint lock was not acquired in time: writers of the type are queued up behind each other.
    private static bool IsLockTimeout(SqlException exception) =>
        exception.Has(SqlServerErrors.LockTimeout) || exception.Has(SqlServerErrors.LockRequestTimeout);
}
