using dddlib.Persistence.SqlServer;

namespace dddlib.Persistence.EventDispatcher.SqlServer;

/// <summary>
/// The schema script for the SQL Server event dispatcher. Run it after the dddlib.Persistence scripts.
/// </summary>
public static class SqlServerEventDispatcherScripts
{
    public const string Name = "06-SqlServerEventDispatcher.sql";

    private const string ResourceName = "dddlib.Persistence.EventDispatcher.Scripts." + Name;

    /// <summary>
    /// Reads the script, optionally rewritten for a schema other than <c>dbo</c>.
    /// </summary>
    public static string Read(string schema = "dbo")
    {
        ArgumentException.ThrowIfNullOrEmpty(schema);

        using var stream = typeof(SqlServerEventDispatcherScripts).Assembly.GetManifestResourceStream(ResourceName)!;
        using var reader = new StreamReader(stream);

        var script = reader.ReadToEnd();
        return schema == "dbo" ? script : script.Replace("[dbo]", SqlServerIdentifier.Quote(schema), StringComparison.Ordinal);
    }
}
