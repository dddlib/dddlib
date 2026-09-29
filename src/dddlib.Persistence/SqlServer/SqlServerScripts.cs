using System.Reflection;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// The schema scripts for the SQL Server implementations, in the order they must be run. They are shipped as
/// content in the package to be run manually before first use; this class exposes the same text for tooling.
/// </summary>
public static class SqlServerScripts
{
    private const string ResourcePrefix = "dddlib.Persistence.Scripts.";

    /// <summary>
    /// Gets the script names in the order they must be run.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } =
        typeof(SqlServerScripts).Assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(static name => name[ResourcePrefix.Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Reads a script, optionally rewritten for a schema other than <c>dbo</c>.
    /// </summary>
    public static string Read(string name, string schema = "dbo")
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(schema);

        using var stream = typeof(SqlServerScripts).Assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new ArgumentException($"Unknown script '{name}'.", nameof(name));
        using var reader = new StreamReader(stream);

        var script = reader.ReadToEnd();
        return schema == "dbo" ? script : script.Replace("[dbo]", SqlServerIdentifier.Quote(schema), StringComparison.Ordinal);
    }

    /// <summary>
    /// Splits a script into the batches delimited by <c>GO</c> lines.
    /// </summary>
    public static IReadOnlyList<string> SplitBatches(string script)
    {
        ArgumentNullException.ThrowIfNull(script);

        return script
            .Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .Aggregate(
                new List<List<string>> { new() },
                (batches, line) =>
                {
                    if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
                    {
                        batches.Add([]);
                    }
                    else
                    {
                        batches[^1].Add(line);
                    }

                    return batches;
                })
            .Select(static lines => string.Join(Environment.NewLine, lines))
            .Where(static batch => !string.IsNullOrWhiteSpace(batch))
            .ToArray();
    }
}
