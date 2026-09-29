using System.Text.Json;

namespace dddlib.Sdk;

internal static class JsonSerialization
{
    // System.Text.Json writes DateTime and DateTimeOffset as ISO 8601 round-trip strings by default.
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.General);
}
