using dddlib.Persistence.Projections.Memory;

namespace dddlib.Persistence.Projections.Tests.Integration;

public class MemoryRepositoryTests
{
    [Test]
    public async Task AddOrUpdateThenGet()
    {
        var repository = new MemoryRepository<string, View>();

        await repository.AddOrUpdateAsync("a", new View("one"));
        await repository.AddOrUpdateAsync("a", new View("two"));

        await Assert.That(await repository.GetAsync("a")).IsEqualTo(new View("two"));
    }

    [Test]
    public async Task GetMissingReturnsNull()
    {
        var repository = new MemoryRepository<string, View>();

        await Assert.That(await repository.GetAsync("missing")).IsNull();
    }

    [Test]
    public async Task RemoveDeletesTheView()
    {
        var repository = new MemoryRepository<string, View>();
        await repository.AddOrUpdateAsync("a", new View("one"));

        await repository.RemoveAsync("a");
        await repository.RemoveAsync("missing");

        await Assert.That(await repository.GetAsync("a")).IsNull();
    }

    [Test]
    public async Task PurgeDeletesEveryView()
    {
        var repository = new MemoryRepository<string, View>();
        await repository.AddOrUpdateAsync("a", new View("one"));
        await repository.AddOrUpdateAsync("b", new View("two"));

        await repository.PurgeAsync();

        await Assert.That(await repository.GetAllAsync().ToListAsync()).IsEmpty();
    }

    [Test]
    public async Task BulkUpdateAddsUpdatesAndRemoves()
    {
        var repository = new MemoryRepository<string, View>();
        await repository.AddOrUpdateAsync("a", new View("one"));
        await repository.AddOrUpdateAsync("b", new View("two"));

        await repository.BulkUpdateAsync([new("a", new View("uno")), new("c", new View("three"))], ["b", "missing"]);

        await Assert.That(await repository.GetAsync("a")).IsEqualTo(new View("uno"));
        await Assert.That(await repository.GetAsync("b")).IsNull();
        await Assert.That(await repository.GetAsync("c")).IsEqualTo(new View("three"));
    }

    [Test]
    public async Task GetAllReturnsEveryView()
    {
        var repository = new MemoryRepository<string, View>();
        await repository.AddOrUpdateAsync("a", new View("one"));
        await repository.AddOrUpdateAsync("b", new View("two"));

        var all = await repository.GetAllAsync().ToListAsync();

        await Assert.That(all.OrderBy(static view => view.Key).Select(static view => view.Value.Name)).IsEquivalentTo(["one", "two"]);
    }

    [Test]
    public async Task CustomIdentityComparer()
    {
        var repository = new MemoryRepository<string, View>(StringComparer.OrdinalIgnoreCase);

        await repository.AddOrUpdateAsync("a", new View("one"));

        await Assert.That(await repository.GetAsync("A")).IsEqualTo(new View("one"));
        await Assert.That(repository.Comparer).IsSameReferenceAs(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record View(string Name);
}
