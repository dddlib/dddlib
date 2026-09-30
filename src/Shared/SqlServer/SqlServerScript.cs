using System.Globalization;
using System.Text.RegularExpressions;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// One numbered script of the dddlib schema (<c>dddlibNN.sql</c>), written against the <c>[dbo]</c> schema.
/// </summary>
internal sealed partial class SqlServerScript
{
    public SqlServerScript(int version, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentNullException.ThrowIfNull(text);

        this.Version = version;
        this.Text = text;
    }

    public int Version { get; }

    public string Text { get; }

    /// <summary>
    /// Gets the script rewritten for a schema other than <c>dbo</c>.
    /// </summary>
    public string For(string schema)
    {
        var quoted = SqlServerIdentifier.Quote(schema);
        return quoted == "[dbo]" ? this.Text : this.Text.Replace("[dbo]", quoted, StringComparison.Ordinal);
    }

    /// <summary>
    /// Splits a script into the batches delimited by <c>GO</c> lines: a line holding only <c>GO</c>, in any case,
    /// optionally followed by a <c>--</c> comment. Splitting is by line, as in sqlcmd, so a <c>GO</c> line inside a block
    /// comment or a string also splits. <c>GO</c> with a repeat count is not supported.
    /// </summary>
    public static IReadOnlyList<string> SplitBatches(string script)
    {
        ArgumentNullException.ThrowIfNull(script);

        var batches = new List<string>();
        var lines = new List<string>();

        foreach (var line in script.Split('\n').Select(static line => line.TrimEnd('\r')))
        {
            if (Separator().IsMatch(line))
            {
                batches.Add(string.Join(Environment.NewLine, lines));
                lines.Clear();
            }
            else if (RepeatedSeparator().IsMatch(line))
            {
                throw new NotSupportedException(
                    string.Format(CultureInfo.InvariantCulture, "The batch separator '{0}' has a repeat count, which is not supported.", line.Trim()));
            }
            else
            {
                lines.Add(line);
            }
        }

        batches.Add(string.Join(Environment.NewLine, lines));

        return batches.Where(static batch => !string.IsNullOrWhiteSpace(batch)).ToArray();
    }

    [GeneratedRegex(@"^\s*GO\s*(--.*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Separator();

    [GeneratedRegex(@"^\s*GO\s+\d+\s*(--.*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedSeparator();
}
