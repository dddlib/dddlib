namespace dddlib.Persistence.Sdk;

/// <summary>
/// Checks the correlation id a caller supplies for a commit of memento saves.
/// </summary>
internal static class CorrelationIds
{
    /// <summary>
    /// Throws an <see cref="ArgumentException"/> for an empty correlation id, which would correlate nothing.
    /// </summary>
    public static void Validate(Guid correlationId, string paramName)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("The correlation id cannot be empty.", paramName);
        }
    }
}
