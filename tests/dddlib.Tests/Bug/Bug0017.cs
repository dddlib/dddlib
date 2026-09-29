using dddlib.Runtime;

namespace dddlib.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/17
// An entity with two natural keys must fail with a runtime exception that is not wrapped in another exception.
public class Bug0017
{
    [Test]
    public async Task ShouldThrow()
    {
        var action = () => { _ = new Thing(); };

        var exception = await Assert.That(action).Throws<RuntimeException>();
        await Assert.That(exception!.InnerException).IsNull();
    }

    public class Thing : Entity
    {
        [NaturalKey]
        public string? NaturalKey { get; set; }

        [NaturalKey]
        public string? AnotherNaturalKey { get; set; }
    }
}
