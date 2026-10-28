using System.Text.Json;
using dddlib.Persistence.SqlServer;

namespace dddlib.Persistence.Tests.Unit;

// The metadata written with every event: dddlib's hostname and timestamp, and a caller's entries beside them
// (https://github.com/dddlib/dddlib/issues/52).
public class SqlServerEventsTests
{
    [Test]
    public async Task MetadataHasTheHostnameAndTimestamp()
    {
        using var metadata = JsonDocument.Parse(SqlServerEvents.CreateMetadata());

        await Assert.That(metadata.RootElement.EnumerateObject().Select(static property => property.Name)).IsEquivalentTo(["hostname", "timestamp"]);
        await Assert.That(metadata.RootElement.GetProperty("hostname").GetString()).IsEqualTo(Environment.MachineName);
    }

    [Test]
    public async Task CallerEntriesAreWrittenBesideTheHostnameAndTimestamp()
    {
        using var metadata = JsonDocument.Parse(SqlServerEvents.CreateMetadata(new Dictionary<string, string> { ["tenant"] = "acme" }));

        await Assert.That(metadata.RootElement.EnumerateObject().Select(static property => property.Name)).IsEquivalentTo(["hostname", "timestamp", "tenant"]);
        await Assert.That(metadata.RootElement.GetProperty("tenant").GetString()).IsEqualTo("acme");
    }

    [Test]
    [Arguments("hostname")]
    [Arguments("timestamp")]
    public async Task CallerEntryCannotReplaceDddlibsOwn(string key)
    {
        await Assert.That(() => SqlServerEvents.CreateMetadata(new Dictionary<string, string> { [key] = "value" }))
            .Throws<ArgumentException>()
            .WithMessageContaining($"The metadata key '{key}' is reserved for dddlib.");
    }

    [Test]
    public async Task CallerEntryMustHaveAValue()
    {
        await Assert.That(() => SqlServerEvents.CreateMetadata(new Dictionary<string, string> { ["tenant"] = null! }))
            .Throws<ArgumentException>()
            .WithMessageContaining("The metadata entry 'tenant' has no value.");
    }
}
