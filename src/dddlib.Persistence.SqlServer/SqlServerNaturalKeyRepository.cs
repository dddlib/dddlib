using System.Data;
using System.Transactions;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

public sealed class SqlServerNaturalKeyRepository : INaturalKeyRepository
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
        catch (SqlException ex) when (ex.Has(SqlServerErrors.PrimaryKeyViolation) || ex.Has(SqlServerErrors.UniqueIndexViolation))
        {
            // Another caller added a key at the same checkpoint first; the caller synchronizes and retries.
            return null;
        }
    }

    public async Task RemoveAsync(Guid naturalKeyIdentity, CancellationToken cancellationToken = default)
    {
        const int attempts = 3;

        for (var attempt = 1; ; attempt++)
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
                return;
            }
            catch (SqlException ex) when (attempt < attempts && ex.Has(SqlServerErrors.PrimaryKeyViolation))
            {
                // A concurrent add or removal took the checkpoint; try again.
            }
        }
    }
}
