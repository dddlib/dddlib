using System.Data;
using System.Globalization;
using System.Reflection;
using System.Transactions;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// Creates or upgrades the dddlib schema from the numbered scripts embedded in this assembly, recording each applied
/// version in the schema's <c>Versions</c> table. Every package that talks to SQL Server carries the same scripts, so
/// installing through any of them installs the whole schema.
/// </summary>
internal static class SqlServerSchemaInstaller
{
    private const string ResourcePrefix = "dddlib.SqlServer.Scripts.dddlib";
    private const string ResourceSuffix = ".sql";

    /// <summary>
    /// Gets the scripts embedded in this assembly, in version order.
    /// </summary>
    public static IReadOnlyList<SqlServerScript> Scripts { get; } = Validate(Load());

    /// <summary>
    /// Gets the schema version this assembly requires: the version of its latest script.
    /// </summary>
    public static int RequiredVersion => Scripts[^1].Version;

    /// <summary>
    /// Gets the name and version of this assembly, recorded with each version it applies.
    /// </summary>
    public static string Description { get; } = GetDescription();

    public static Task<(int Version, int RequiredVersion, int MinimumRequiredVersion)> EnsureAsync(string connectionString, string schema, CancellationToken cancellationToken) =>
        EnsureAsync(connectionString, schema, Scripts, cancellationToken);

    /// <summary>
    /// Applies the scripts the schema is missing, in order, in one transaction under an exclusive application lock on
    /// the schema, so concurrent callers apply each version once. A schema already ahead of these scripts is left as
    /// it is and reported, not refused: older code keeps working against a newer schema during a rolling upgrade,
    /// until a script records that the schema no longer supports it.
    /// </summary>
    public static async Task<(int Version, int RequiredVersion, int MinimumRequiredVersion)> EnsureAsync(string connectionString, string schema, IReadOnlyList<SqlServerScript> scripts, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        var quotedSchema = SqlServerIdentifier.Quote(schema);
        scripts = Validate(scripts);
        var required = scripts[^1].Version;

        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var command = CreateCommand(connection, transaction, @"DECLARE @Result INT;
EXEC @Result = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = -1;
IF @Result < 0
    THROW 50500, 'Failed to acquire the schema upgrade lock.', 1;
IF SCHEMA_ID(@Schema) IS NULL
    EXEC (N'CREATE SCHEMA ' + @QuotedSchema);"))
        {
            command.Parameters.Add("@Resource", SqlDbType.NVarChar, 255).Value = string.Concat("dddlib.Schema.", schema);
            command.Parameters.Add("@Schema", SqlDbType.NVarChar, 128).Value = schema;
            command.Parameters.Add("@QuotedSchema", SqlDbType.NVarChar, 130).Value = quotedSchema;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var applied = await ReadVersionsAsync(connection, transaction, quotedSchema, cancellationToken).ConfigureAwait(false);
        ThrowIfTooOld(quotedSchema, applied.Version, applied.MinimumRequiredVersion, required);

        var current = applied.Version;
        foreach (var script in scripts.Where(script => script.Version > current))
        {
            foreach (var batch in SqlServerScript.SplitBatches(script.For(schema)))
            {
                await using var command = CreateCommand(connection, transaction, batch);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // Record what was applied, and fill in the text of versions that were applied by hand.
        foreach (var script in scripts.Where(script => script.Version > current || (applied.HasScript.TryGetValue(script.Version, out var hasScript) && !hasScript)))
        {
            await using var command = CreateCommand(connection, transaction, string.Concat(
                "MERGE ", quotedSchema, @".[Versions] AS [Target]
USING (SELECT @Version AS [Version]) AS [Source]
ON [Target].[Version] = [Source].[Version]
WHEN MATCHED AND [Target].[Script] IS NULL THEN
    UPDATE SET [Description] = @Description, [Script] = @Script
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Version], [Description], [Script])
    VALUES (@Version, @Description, @Script);"));

            command.Parameters.Add("@Version", SqlDbType.Int).Value = script.Version;
            command.Parameters.Add("@Description", SqlDbType.VarChar, -1).Value = Description;
            command.Parameters.Add("@Script", SqlDbType.NVarChar, -1).Value = script.For(schema);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (current < required)
        {
            // A script that was applied may have raised the oldest required version the schema supports.
            applied = await ReadVersionsAsync(connection, transaction, quotedSchema, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return (applied.Version, required, applied.MinimumRequiredVersion);
    }

    /// <summary>
    /// Gets every script rewritten for the schema, preceded by a batch that creates the schema, for running by hand or
    /// from a migration tool that understands <c>GO</c>.
    /// </summary>
    public static string GetScript(string schema)
    {
        var quotedSchema = SqlServerIdentifier.Quote(schema);

        return string.Concat(
            string.Format(
                CultureInfo.InvariantCulture,
                "IF SCHEMA_ID(N'{0}') IS NULL{1}    EXEC (N'CREATE SCHEMA {2}');{1}GO{1}{1}",
                schema,
                Environment.NewLine,
                quotedSchema),
            string.Join(Environment.NewLine, Scripts.Select(script => script.For(schema).TrimEnd() + Environment.NewLine)));
    }

    /// <summary>
    /// Reads the version the schema is at without changing anything, for a process that reports on the schema but does
    /// not upgrade it.
    /// </summary>
    public static async Task<(int Version, int RequiredVersion, int MinimumRequiredVersion)> GetVersionAsync(string connectionString, string schema, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        var quotedSchema = SqlServerIdentifier.Quote(schema);

        var (version, minimumRequiredVersion) = await ReadVersionAsync(connectionString, quotedSchema, cancellationToken).ConfigureAwait(false);
        return (version, RequiredVersion, minimumRequiredVersion);
    }

    /// <summary>
    /// Reads the version the schema is at, zero when the <c>Versions</c> table does not exist, and the oldest required
    /// version the schema still supports.
    /// </summary>
    public static async Task<(int Version, int MinimumRequiredVersion)> ReadVersionAsync(string connectionString, string quotedSchema, CancellationToken cancellationToken)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var applied = await ReadVersionsAsync(connection, null, quotedSchema, cancellationToken).ConfigureAwait(false);
        return (applied.Version, applied.MinimumRequiredVersion);
    }

    /// <summary>
    /// Fails when the schema no longer supports a package that requires this version: a later script removed or changed
    /// something the package uses, and recorded the oldest required version that still works.
    /// </summary>
    public static void ThrowIfTooOld(string quotedSchema, int version, int minimumRequiredVersion, int requiredVersion)
    {
        if (requiredVersion >= minimumRequiredVersion)
        {
            return;
        }

        throw new PersistenceException(
            string.Format(
                CultureInfo.InvariantCulture,
                @"The SQL Server schema {0} is at version {1}, which supports packages that require version {2} or later, but {3} requires version {4}.
To fix this issue, update the dddlib packages this process uses to the version that upgraded the schema, or to any version that requires schema version {2} or later.
Further information: https://github.com/dddlib/dddlib/blob/main/docs/persistence/sql-server.md",
                quotedSchema,
                version,
                minimumRequiredVersion,
                Description,
                requiredVersion));
    }

    private static async Task<AppliedVersions> ReadVersionsAsync(SqlConnection connection, SqlTransaction? transaction, string quotedSchema, CancellationToken cancellationToken)
    {
        var table = string.Concat(quotedSchema, ".[Versions]");

        await using var command = CreateCommand(connection, transaction, string.Concat(
            "IF OBJECT_ID(N'", table, "', N'U') IS NOT NULL", Environment.NewLine,
            "    SELECT [Version], CAST(CASE WHEN [Script] IS NULL THEN 0 ELSE 1 END AS BIT), [MinimumRequiredVersion] FROM ", table, ";"));

        var hasScript = new Dictionary<int, bool>();
        var minimumRequiredVersion = 1;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            hasScript.Add(reader.GetInt32(0), reader.GetBoolean(1));
            if (!await reader.IsDBNullAsync(2, cancellationToken).ConfigureAwait(false))
            {
                minimumRequiredVersion = Math.Max(minimumRequiredVersion, reader.GetInt32(2));
            }
        }

        return new AppliedVersions(hasScript, minimumRequiredVersion);
    }

    private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction? transaction, string commandText)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;

        // Schema changes can take longer than a command timeout allows; the caller cancels with the token instead.
        command.CommandTimeout = 0;
        return command;
    }

    private static List<SqlServerScript> Load()
    {
        var assembly = typeof(SqlServerSchemaInstaller).Assembly;

        return assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .Select(name =>
            {
                var number = name[ResourcePrefix.Length..^ResourceSuffix.Length];
                if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, "Cannot infer the version number of the script '{0}'.", name));
                }

                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                return new SqlServerScript(version, reader.ReadToEnd());
            })
            .ToList();
    }

    private static SqlServerScript[] Validate(IEnumerable<SqlServerScript> scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);

        var ordered = scripts.OrderBy(static script => script.Version).ToArray();
        if (ordered.Length == 0 || ordered[0].Version != 1)
        {
            throw new InvalidOperationException("The SQL Server schema scripts must start at version 1.");
        }

        if (ordered.Where((script, index) => script.Version != index + 1).Any())
        {
            throw new InvalidOperationException("The SQL Server schema script versions must be contiguous.");
        }

        return ordered;
    }

    private static string GetDescription()
    {
        var assembly = typeof(SqlServerSchemaInstaller).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();

        return string.Concat(assembly.GetName().Name, " ", version);
    }

    /// <summary>
    /// The rows of the <c>Versions</c> table: whether each applied version has its script text, and the oldest required
    /// version any of them says the schema still supports.
    /// </summary>
    private sealed record AppliedVersions(Dictionary<int, bool> HasScript, int MinimumRequiredVersion)
    {
        public int Version => this.HasScript.Count == 0 ? 0 : this.HasScript.Keys.Max();
    }
}
