using System.Collections.ObjectModel;
using System.Text.Json;
using dddlib.Sdk;

namespace dddlib.Persistence.Sdk;

/// <summary>
/// The metadata stored with each event: dddlib's <c>hostname</c> and <c>timestamp</c>, and a caller's entries beside
/// them. Every store builds and reads it here, so the feed and the event dispatcher see the same entries in memory and
/// on SQL Server.
/// </summary>
internal static class EventMetadata
{
    public const string HostnameKey = "hostname";
    public const string TimestampKey = "timestamp";

    public static string Hostname { get; } = Environment.MachineName;

    /// <summary>
    /// Creates the metadata of one event: dddlib's entries, at <paramref name="timestamp"/>, then the caller's, with the
    /// event's own merged over them. Neither can replace the hostname or timestamp.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Create(
        IReadOnlyDictionary<string, string>? metadata,
        IReadOnlyDictionary<string, string>? eventMetadata,
        DateTime timestamp)
    {
        Validate(metadata, nameof(metadata));
        Validate(eventMetadata, nameof(eventMetadata));

        var entries = new Dictionary<string, string>
        {
            [HostnameKey] = Hostname,
            [TimestampKey] = FormatTimestamp(timestamp),
        };

        foreach (var (key, value) in metadata ?? ReadOnlyDictionary<string, string>.Empty)
        {
            entries[key] = value;
        }

        foreach (var (key, value) in eventMetadata ?? ReadOnlyDictionary<string, string>.Empty)
        {
            entries[key] = value;
        }

        return new ReadOnlyDictionary<string, string>(entries);
    }

    /// <summary>
    /// Throws when an entry would replace dddlib's own or has no value, so that nothing is written.
    /// </summary>
    public static void Validate(IReadOnlyDictionary<string, string>? metadata, string parameterName)
    {
        if (metadata is null)
        {
            return;
        }

        foreach (var (key, value) in metadata)
        {
            if (key is HostnameKey or TimestampKey)
            {
                throw new ArgumentException($"The metadata key '{key}' is reserved for dddlib.", parameterName);
            }

            if (value is null)
            {
                throw new ArgumentException($"The metadata entry '{key}' has no value.", parameterName);
            }
        }
    }

    /// <summary>
    /// Reads stored metadata: the string entries of the JSON object. Anything else is absent rather than an error, which
    /// would stop a feed or a dispatcher at that event each time it is read again.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(string? json)
    {
        if (json is null)
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ReadOnlyDictionary<string, string>.Empty;
            }

            var metadata = new Dictionary<string, string>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    metadata[property.Name] = property.Value.GetString()!;
                }
            }

            return new ReadOnlyDictionary<string, string>(metadata);
        }
        catch (JsonException)
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }
    }

    // As the JSON serializer writes a DateTime, so that the timestamp reads the same whichever store wrote it.
    private static string FormatTimestamp(DateTime timestamp) =>
        JsonSerializer.SerializeToElement(timestamp, JsonSerialization.Options).GetString()!;
}
