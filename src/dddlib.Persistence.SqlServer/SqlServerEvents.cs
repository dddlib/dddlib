using System.Buffers;
using System.Data;
using System.Text;
using System.Text.Json;
using dddlib.Persistence.Sdk;
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
    private static readonly SqlMetaData[] EventColumns =
    [
        new("Index", SqlDbType.Int),
        new("TypeId", SqlDbType.Int),
        new("Payload", SqlDbType.NVarChar, -1),
    ];

    private static readonly SqlMetaData[] StreamEventColumns =
    [
        new("StreamId", SqlDbType.UniqueIdentifier),
        new("Index", SqlDbType.Int),
        new("TypeId", SqlDbType.Int),
        new("Payload", SqlDbType.NVarChar, -1),
    ];

    private static readonly SqlMetaData[] EventMetadataColumns =
    [
        new("StreamId", SqlDbType.UniqueIdentifier),
        new("Index", SqlDbType.Int),
        new("Metadata", SqlDbType.NVarChar, -1),
    ];

    /// <summary>
    /// Adds the events of one stream to the records of a <c>StreamEventList</c> table-valued parameter, indexed from 1
    /// within the stream.
    /// </summary>
    public static async Task AddStreamRecordsAsync(List<SqlDataRecord> records, Guid streamId, IReadOnlyList<object> events, SqlServerTypeCache typeCache, CancellationToken cancellationToken)
    {
        for (var index = 0; index < events.Count; index++)
        {
            var @event = events[index];
            var record = new SqlDataRecord(StreamEventColumns);
            record.SetGuid(0, streamId);
            record.SetInt32(1, index + 1);
            record.SetInt32(2, await typeCache.GetTypeIdAsync(@event.GetType(), cancellationToken).ConfigureAwait(false));
            record.SetString(3, JsonSerializer.Serialize(@event, @event.GetType(), JsonSerialization.Options));
            records.Add(record);
        }
    }

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
    public static string CreateMetadata(IReadOnlyDictionary<string, string>? metadata = null) =>
        CreateMetadata(metadata, null, DateTime.UtcNow);

    /// <summary>
    /// Creates the metadata stored with one event: as <see cref="CreateMetadata(IReadOnlyDictionary{string, string}?)"/>,
    /// at <paramref name="timestamp"/>, with the event's own entries merged over the caller's. Neither can replace the
    /// hostname or timestamp.
    /// </summary>
    public static string CreateMetadata(IReadOnlyDictionary<string, string>? metadata, IReadOnlyDictionary<string, string>? eventMetadata, DateTime timestamp)
    {
        var entries = EventMetadata.Create(metadata, eventMetadata, timestamp);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JsonSerialization.Options.Encoder }))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in entries)
            {
                writer.WriteString(key, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Adds a record of an <c>EventMetadataList</c> table-valued parameter for each of one stream's events that has
    /// entries of its own, indexed from 1 within the stream as its events are; the other events take the metadata of
    /// the append. Validates every entry, so call it before writing.
    /// </summary>
    public static void AddEventMetadataRecords(
        List<SqlDataRecord> records,
        Guid streamId,
        IReadOnlyDictionary<string, string>? metadata,
        IReadOnlyList<IReadOnlyDictionary<string, string>?> eventMetadata,
        DateTime timestamp)
    {
        for (var index = 0; index < eventMetadata.Count; index++)
        {
            var own = eventMetadata[index];
            if (own is null || own.Count == 0)
            {
                continue;
            }

            var record = new SqlDataRecord(EventMetadataColumns);
            record.SetGuid(0, streamId);
            record.SetInt32(1, index + 1);
            record.SetString(2, CreateMetadata(metadata, own, timestamp));
            records.Add(record);
        }
    }

    /// <summary>
    /// Adds the <c>@EventMetadata</c> parameter when there are records: SqlClient rejects an empty enumeration, and an
    /// omitted table-valued parameter is an empty table.
    /// </summary>
    public static void AddEventMetadataParameter(SqlCommand command, string quotedSchema, List<SqlDataRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        var parameter = command.Parameters.Add("@EventMetadata", SqlDbType.Structured);
        parameter.TypeName = string.Concat(quotedSchema, ".[EventMetadataList]");
        parameter.Value = records;
    }
}
