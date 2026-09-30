using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Transactions;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// Maps types to the integer identifiers they are stored under in the <c>Types</c> table, caching in memory.
/// </summary>
public sealed class SqlServerTypeCache : ITypeCache
{
    private readonly ConcurrentDictionary<int, string> typeNames = new();
    private readonly ConcurrentDictionary<Type, int> typeIds = new();
    private readonly string connectionString;
    private readonly string schema;

    public SqlServerTypeCache(string connectionString, string schema = "dbo")
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        this.connectionString = connectionString;
        this.schema = SqlServerIdentifier.Quote(schema);
    }

    public async Task<int> GetTypeIdAsync(Type type, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (this.typeIds.TryGetValue(type, out var typeId))
        {
            return typeId;
        }

        await this.TryAddTypeAsync(type, cancellationToken).ConfigureAwait(false);
        await this.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        return this.typeIds.TryGetValue(type, out typeId)
            ? typeId
            : throw new PersistenceException(string.Format(CultureInfo.InvariantCulture, "Unable to register the type '{0}'.", type));
    }

    public async Task<string> GetTypeNameAsync(int typeId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(typeId);

        if (!this.typeNames.TryGetValue(typeId, out var typeName))
        {
            await this.SynchronizeAsync(cancellationToken).ConfigureAwait(false);
        }

        return typeName ?? (this.typeNames.TryGetValue(typeId, out typeName)
            ? typeName
            : throw new PersistenceException(string.Format(CultureInfo.InvariantCulture, "Unknown type id '{0}'.", typeId)));
    }

    public async Task<Type> GetTypeAsync(int typeId, CancellationToken cancellationToken = default)
    {
        var typeName = await this.GetTypeNameAsync(typeId, cancellationToken).ConfigureAwait(false);

        return TypeNameResolver.ResolveOrThrow(typeName);
    }

    private async Task TryAddTypeAsync(Type type, CancellationToken cancellationToken)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[TryAddType]");
        command.Parameters.Add("@Name", SqlDbType.VarChar, 511).Value = type.GetSerializedName();

        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(this.connectionString);
        await using var command = connection.CreateCommand();

        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = string.Concat(this.schema, ".[GetTypes]");

        await SqlServerSchemaCheck.EnsureCompatibleAsync(this.connectionString, this.schema, cancellationToken).ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var typeId = reader.GetInt32(0);
            var typeName = reader.GetString(1);
            this.typeNames.TryAdd(typeId, typeName);

            if (TypeNameResolver.Resolve(typeName) is { } type)
            {
                this.typeIds.TryAdd(type, typeId);
            }
        }
    }
}
