namespace dddlib.Tests.Bug;

// https://github.com/dddlib/dddlib/issues/1
// An entity with a value-typed natural key (Guid) could not be instantiated.
public class Bug0001
{
    [Test]
    public async Task ShouldNotThrow()
    {
        var action = () => { _ = new Todo(Guid.NewGuid()); };

        await Assert.That(action).ThrowsNothing();
    }

    public class Todo : Entity
    {
        public Todo(Guid id)
        {
            this.Id = id;
        }

        [NaturalKey]
        public Guid Id { get; set; }
    }
}
