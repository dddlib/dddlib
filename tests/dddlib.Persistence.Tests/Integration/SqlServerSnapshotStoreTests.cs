using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Tests.Integration;

public class SqlServerSnapshotStoreTests : SqlServerIntegration
{
    [Test]
    public async Task TrySaveSnapshot()
    {
        var snapshotStore = new SqlServerSnapshotStore(this.ConnectionString);
        var streamId = Guid.NewGuid();
        var expectedSnapshot = new Snapshot(4, new Memento { Id = 2, Name = "example" });

        await snapshotStore.PutSnapshotAsync(streamId, expectedSnapshot);
        var actualSnapshot = await snapshotStore.GetSnapshotAsync(streamId);

        await Assert.That(actualSnapshot).IsNotNull();
        await Assert.That(actualSnapshot!.StreamRevision).IsEqualTo(4);
        await Assert.That(actualSnapshot.Memento).IsTypeOf<Memento>();
        await Assert.That(MementoJson.Of(actualSnapshot.Memento)).IsEqualTo(MementoJson.Of(expectedSnapshot.Memento));
    }

    [Test]
    public async Task TryUpdateSnapshotRevision()
    {
        var snapshotStore = new SqlServerSnapshotStore(this.ConnectionString);
        var streamId = Guid.NewGuid();
        var firstSnapshot = new Snapshot(4, new Memento { Id = 2, Name = "first" });
        var secondSnapshot = new Snapshot(8, new Memento { Id = 2, Name = "second" });

        await snapshotStore.PutSnapshotAsync(streamId, firstSnapshot);
        await snapshotStore.PutSnapshotAsync(streamId, secondSnapshot);
        var actualSnapshot = await snapshotStore.GetSnapshotAsync(streamId);

        await Assert.That(actualSnapshot).IsNotNull();
        await Assert.That(actualSnapshot!.StreamRevision).IsEqualTo(8);
        await Assert.That(MementoJson.Of(actualSnapshot.Memento)).IsEqualTo(MementoJson.Of(secondSnapshot.Memento));
    }

    private sealed class Memento
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }
}
