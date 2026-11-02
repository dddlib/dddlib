using System.Buffers;
using System.Data;
using System.Text;
using System.Text.Json;
using dddlib.Sdk;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;

namespace dddlib.Persistence.SqlServer;

/// <summary>
/// Builds the <c>EventList</c> table-valued parameter and the metadata with which the event store and the memento
/// repositories write events.
/// </summary>
internal static class SqlServerEvents
{
    private const string HostnameKey = "hostname";
    private const string TimestampKey = "timestamp";

    private static readonly string Hostname = Environment.MachineName;
    private static readonly SqlMetaData[] EventColumns =
    [
        new("Index", SqlDbType.Int),
        new("TypeId", SqlDbType.Int),
        new("Payload", SqlDbType.NVarChar, -1),
    ];

    public static async Task<List<SqlDataRecord>> ToRecordsAsync(IReadOnlyList<object> events, SqlServerTypeCache typeCache, CancellationToken cancellationToken)
    {
        var records = new List<SqlDataRecord>(events.Count);
        foreach (var @event in events)
        {
            var record = new SqlDataRecord(EventColumns);
            record.SetInt32(0, records.Count + 1);
            record.SetInt32(1, await typeCache.GetTypeIdAsync(@event.GetType(), cancellationToken).ConfigureAwait(false));
            record.SetString(2, JsonSerializer.Serialize(@event, @event.GetType(), JsonSerialization.Options));
            records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Adds the <c>@Events</c> parameter. The records must not be empty: SqlClient rejects an empty enumeration, and
    /// an omitted table-valued parameter is an empty table anyway.
    /// </summary>
    public static void AddEventsParameter(SqlCommand command, string quotedSchema, List<SqlDataRecord> records)
    {
        var parameter = command.Parameters.Add("@Events", SqlDbType.Structured);
        parameter.TypeName = string.Concat(quotedSchema, ".[EventList]");
        parameter.Value = records;
    }

    /// <summary>
    /// Creates the metadata stored with the events: one JSON object with the hostname and timestamp, and the caller's
    /// entries beside them. A caller's entry cannot replace the hostname or timestamp.
    /// </summary>
    public static string CreateMetadata(IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (metadata is not null)
        {
            foreach (var (key, value) in metadata)
            {
                if (key is HostnameKey or TimestampKey)
                {
                    throw new ArgumentException($"The metadata key '{key}' is reserved for dddlib.", nameof(metadata));
                }

                if (value is null)
                {
                    throw new ArgumentException($"The metadata entry '{key}' has no value.", nameof(metadata));
                }
            }
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JsonSerialization.Options.Encoder }))
        {
            writer.WriteStartObject();
            writer.WriteString(HostnameKey, Hostname);
            writer.WritePropertyName(TimestampKey);
            JsonSerializer.Serialize(writer, DateTime.UtcNow, JsonSerialization.Options);
            foreach (var (key, value) in metadata ?? new Dictionary<string, string>())
            {
                writer.WriteString(key, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
