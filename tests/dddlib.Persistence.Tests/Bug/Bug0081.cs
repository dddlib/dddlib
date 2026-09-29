using dddlib.Persistence.Memory;
using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/81
// An identity without a stream (the natural key was reserved but nothing was committed) is not found.
public class Bug0081
{
    [Test]
    public async Task ShouldThrowForEventStoreRepository()
    {
        var identityMap = new MemoryIdentityMap();
        var repository = new EventStoreRepository(identityMap, new MemoryEventStore(), new MemorySnapshotStore());
        var naturalKey = "key";
        await identityMap.GetOrAddAsync(typeof(Subject), typeof(string), naturalKey);

        Func<Task> action = () => repository.LoadAsync<Subject>(naturalKey);

        await Assert.That(action).Throws<AggregateRootNotFoundException>();
    }

    [Test]
    public async Task ShouldThrowForConventionalRepository()
    {
        var identityMap = new MemoryIdentityMap();
        var repository = new MemoryRepository<Subject>(identityMap);
        var naturalKey = "key";
        await identityMap.GetOrAddAsync(typeof(Subject), typeof(string), naturalKey);

        Func<Task> action = () => repository.LoadAsync(naturalKey);

        await Assert.That(action).Throws<AggregateRootNotFoundException>();
    }

    private sealed class Subject : AggregateRoot
    {
        [NaturalKey]
        public string? NaturalKey { get; set; }
    }
}
