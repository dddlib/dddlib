using System.Data;
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

    public static string CreateMetadata() =>
        JsonSerializer.Serialize(new Metadata(Hostname, DateTime.UtcNow), JsonSerialization.Options);

    private sealed record Metadata(string Hostname, DateTime Timestamp);
}
