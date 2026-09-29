using dddlib.Persistence.Memory;
using dddlib.Runtime;

namespace dddlib.Persistence.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/64
// A null natural key is an argument error naming the property; a natural key type that does not round-trip through
// serialization with value equality is a persistence error wrapping a runtime exception.
// The legacy test used the memento-based repository, which is dropped in v2; the event store repository behaves the same.
public class Bug0064
{
    [Test]
    public async Task ShouldThrowArgumentException()
    {
        var thing = new Thing();
        var repository = new MemoryEventStoreRepository();

        var action = () => repository.SaveAsync(thing);

        var exception = await Assert.That(action).Throws<ArgumentException>();
        await Assert.That(exception!.InnerException).IsNull();
        await Assert.That(exception.Message).Contains("aggregateRoot.SomeKey");
    }

    [Test]
    public async Task ShouldThrowPersistenceException()
    {
        var thing = new Thing { SomeKey = new InvalidKey { Value = "irrelevant" } };
        var repository = new MemoryEventStoreRepository();

        var action = () => repository.SaveAsync(thing);

        var exception = await Assert.That(action).Throws<PersistenceException>();
        await Assert.That(exception!.InnerException).IsNotNull();
        await Assert.That(exception.InnerException).IsTypeOf<RuntimeException>();
    }

    public class Thing : AggregateRoot
    {
        [NaturalKey]
        public InvalidKey? SomeKey { get; set; }
    }

    public class InvalidKey
    {
        public string? Value { get; set; }
    }
}
