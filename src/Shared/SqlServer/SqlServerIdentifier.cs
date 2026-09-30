using System.Globalization;
using System.Text.RegularExpressions;

namespace dddlib.Persistence.SqlServer;

internal static partial class SqlServerIdentifier
{
    /// <summary>
    /// Validates that a schema name is a plain identifier and returns it quoted, so it can be embedded in command text.
    /// </summary>
    public static string Quote(string schema)
    {
        ArgumentException.ThrowIfNullOrEmpty(schema);

        if (!Identifier().IsMatch(schema))
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "The schema name '{0}' is not a valid identifier.", schema),
                nameof(schema));
        }

        return string.Concat("[", schema, "]");
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();
}
