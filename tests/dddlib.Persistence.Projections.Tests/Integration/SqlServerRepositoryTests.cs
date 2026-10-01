using dddlib.Persistence.Projections.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Projections.Tests.Integration;

// Each test uses a projection name of its own, so they share the class's database without seeing each other.
public class SqlServerRepositoryTests : SqlServerIntegration
{
    [Test]
    public async Task AddOrUpdateThenGet()
    {
        var repository = this.NewRepository();

        await repository.AddOrUpdateAsync("a", new View("one"));
        await repository.AddOrUpdateAsync("a", new View("two"));

        await Assert.That(await repository.GetAsync("a")).IsEqualTo(new View("two"));
    }

    [Test]
    public async Task GetMissingReturnsNull()
    {
        var repository = this.NewRepository();

        await Assert.That(await repository.GetAsync("missing")).IsNull();
    }

    [Test]
    public async Task RemoveDeletesTheView()
    {
        var repository = this.NewRepository();
        await repository.AddOrUpdateAsync("a", new View("one"));

        await repository.RemoveAsync("a");
        await repository.RemoveAsync("missing");

        await Assert.That(await repository.GetAsync("a")).IsNull();
    }

    [Test]
    public async Task PurgeDeletesEveryView()
    {
        var repository = this.NewRepository();
        await repository.AddOrUpdateAsync("a", new View("one"));
        await repository.AddOrUpdateAsync("b", new View("two"));

        await repository.PurgeAsync();

        await Assert.That(await repository.GetAllAsync().ToListAsync()).IsEmpty();
    }

    [Test]
    public async Task BulkUpdateAddsUpdatesAndRemoves()
    {
        var repository = this.NewRepository();
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
        var repository = this.NewRepository();
        await repository.AddOrUpdateAsync("a", new View("one"));
        await repository.AddOrUpdateAsync("b", new View("two"));

        var all = await repository.GetAllAsync().ToListAsync();

        await Assert.That(all.Select(static view => view.Key)).IsEquivalentTo(["a", "b"]);
        await Assert.That(all.Select(static view => view.Value.Name)).IsEquivalentTo(["one", "two"]);
    }

    [Test]
    public async Task KeysAreCaseSensitive()
    {
        var repository = this.NewRepository();

        await repository.AddOrUpdateAsync("a", new View("lower"));
        await repository.AddOrUpdateAsync("A", new View("upper"));

        await Assert.That(await repository.GetAsync("a")).IsEqualTo(new View("lower"));
        await Assert.That(await repository.GetAsync("A")).IsEqualTo(new View("upper"));
        await Assert.That(await repository.GetAllAsync().CountAsync()).IsEqualTo(2);
    }

    // Keys are the identity as JSON, so a string key is two characters longer than the string.
    [Test]
    public async Task OverlongKeyIsRejected()
    {
        var repository = this.NewRepository();
        var longest = new string('k', 398);

        await repository.AddOrUpdateAsync(longest, new View("fits"));

        await Assert.That(await repository.GetAsync(longest)).IsEqualTo(new View("fits"));
        await Assert.That(() => repository.AddOrUpdateAsync(new string('k', 399), new View("too long")))
            .Throws<ArgumentException>()
            .WithMessageContaining("a projection view key may be at most 400");
        await Assert.That(() => repository.GetAsync(new string('k', 399))).Throws<ArgumentException>();
    }

    [Test]
    public async Task ProjectionsAreIsolatedByName()
    {
        var first = this.NewRepository();
        var second = this.NewRepository();
        await first.AddOrUpdateAsync("a", new View("first"));
        await second.AddOrUpdateAsync("a", new View("second"));

        await first.PurgeAsync();

        await Assert.That(await first.GetAsync("a")).IsNull();
        await Assert.That(await second.GetAsync("a")).IsEqualTo(new View("second"));
    }

    [Test]
    public async Task IdentitiesNeedNotBeStrings()
    {
        var repository = new SqlServerRepository<Guid, View>(this.ConnectionString, Guid.NewGuid().ToString("N"));
        var id = Guid.NewGuid();

        await repository.AddOrUpdateAsync(id, new View("guid"));

        await Assert.That(await repository.GetAsync(id)).IsEqualTo(new View("guid"));
        await Assert.That((await repository.GetAllAsync().SingleAsync()).Key).IsEqualTo(id);
    }

    private SqlServerRepository<string, View> NewRepository() =>
        new(this.ConnectionString, Guid.NewGuid().ToString("N"));

    private sealed record View(string Name);
}
