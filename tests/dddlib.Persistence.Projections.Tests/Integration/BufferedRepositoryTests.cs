using dddlib.Persistence.Projections.Memory;
using dddlib.Persistence.Projections.Sdk;

namespace dddlib.Persistence.Projections.Tests.Integration;

// The repository a projection's handlers see while a page is applied.
public class BufferedRepositoryTests
{
    [Test]
    public async Task ReadsItsOwnWrites()
    {
        var store = new MemoryRepository<string, View>();
        await store.AddOrUpdateAsync("a", new View("stored"));
        var buffer = new BufferedRepository<string, View>(store);

        await buffer.AddOrUpdateAsync("a", new View("buffered"));
        await buffer.AddOrUpdateAsync("b", new View("new"));

        await Assert.That(await buffer.GetAsync("a")).IsEqualTo(new View("buffered"));
        await Assert.That(await buffer.GetAsync("b")).IsEqualTo(new View("new"));
        await Assert.That(await store.GetAsync("a")).IsEqualTo(new View("stored"));
        await Assert.That(await store.GetAsync("b")).IsNull();
    }

    [Test]
    public async Task ARemoveHidesTheStoredView()
    {
        var store = new MemoryRepository<string, View>();
        await store.AddOrUpdateAsync("a", new View("stored"));
        var buffer = new BufferedRepository<string, View>(store);

        await buffer.RemoveAsync("a");

        await Assert.That(await buffer.GetAsync("a")).IsNull();
        await Assert.That(await buffer.GetAllAsync().ToListAsync()).IsEmpty();
        await Assert.That(await store.GetAsync("a")).IsEqualTo(new View("stored"));
    }

    [Test]
    public async Task LastWritePerKeyWins()
    {
        var store = new MemoryRepository<string, View>();
        var buffer = new BufferedRepository<string, View>(store);

        await buffer.AddOrUpdateAsync("a", new View("one"));
        await buffer.RemoveAsync("a");
        await buffer.RemoveAsync("b");
        await buffer.AddOrUpdateAsync("b", new View("two"));

        await Assert.That(buffer.Changes).Count().IsEqualTo(2);
        await Assert.That(buffer.Changes["a"]).IsNull();
        await Assert.That(buffer.Changes["b"]).IsEqualTo(new View("two"));
    }

    [Test]
    public async Task PurgeHidesTheStore()
    {
        var store = new MemoryRepository<string, View>();
        await store.AddOrUpdateAsync("a", new View("stored"));
        await store.AddOrUpdateAsync("b", new View("stored"));
        var buffer = new BufferedRepository<string, View>(store);
        await buffer.AddOrUpdateAsync("c", new View("before"));

        await buffer.PurgeAsync();
        await buffer.AddOrUpdateAsync("b", new View("after"));

        await Assert.That(buffer.IsPurged).IsTrue();
        await Assert.That(await buffer.GetAsync("a")).IsNull();
        await Assert.That(await buffer.GetAsync("c")).IsNull();
        await Assert.That(await buffer.GetAllAsync().ToListAsync()).IsEquivalentTo([new KeyValuePair<string, View>("b", new View("after"))]);
        await Assert.That(await store.GetAllAsync().CountAsync()).IsEqualTo(2);
    }

    [Test]
    public async Task GetAllMergesBufferAndStore()
    {
        var store = new MemoryRepository<string, View>();
        await store.AddOrUpdateAsync("a", new View("stored"));
        await store.AddOrUpdateAsync("b", new View("stored"));
        await store.AddOrUpdateAsync("c", new View("stored"));
        var buffer = new BufferedRepository<string, View>(store);
        await buffer.AddOrUpdateAsync("b", new View("buffered"));
        await buffer.RemoveAsync("c");
        await buffer.AddOrUpdateAsync("d", new View("buffered"));

        var all = (await buffer.GetAllAsync().ToListAsync()).OrderBy(static view => view.Key).ToList();

        await Assert.That(all.Select(static view => view.Key)).IsEquivalentTo(["a", "b", "d"]);
        await Assert.That(all.Select(static view => view.Value.Name)).IsEquivalentTo(["stored", "buffered", "buffered"]);
    }

    [Test]
    public async Task CommitAppliesTheChanges()
    {
        var store = new MemoryRepository<string, View>();
        await store.AddOrUpdateAsync("a", new View("stored"));
        await store.AddOrUpdateAsync("b", new View("stored"));
        var buffer = new BufferedRepository<string, View>(store);
        await buffer.AddOrUpdateAsync("b", new View("buffered"));
        await buffer.RemoveAsync("a");
        await buffer.AddOrUpdateAsync("c", new View("buffered"));

        await buffer.CommitAsync();

        await Assert.That(await store.GetAsync("a")).IsNull();
        await Assert.That(await store.GetAsync("b")).IsEqualTo(new View("buffered"));
        await Assert.That(await store.GetAsync("c")).IsEqualTo(new View("buffered"));
        await Assert.That(buffer.Changes).IsEmpty();
        await Assert.That(buffer.IsPurged).IsFalse();
    }

    private sealed record View(string Name);
}
