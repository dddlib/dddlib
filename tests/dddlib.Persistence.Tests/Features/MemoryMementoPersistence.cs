using dddlib.Configuration;
using dddlib.Persistence.Memory;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Tests.Features;

// As someone who uses dddlib without event sourcing
// In order to persist aggregate roots without a database
// I need the in-memory memento repository to save and load aggregate roots
public abstract class MemoryMementoPersistence : Feature
{
    public sealed class DefaultMemoryPersistence : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository
            var repository = new MemoryRepository<Subject>();

            // And a natural key value
            var naturalKey = "key";

            // And an instance of an aggregate root with that natural key
            var instance = new Subject(naturalKey);

            // When that instance is saved to the repository
            await repository.SaveAsync(instance);

            // And an other instance is loaded from the repository
            var otherInstance = await repository.LoadAsync(instance.NaturalKey!);

            // Then that instance should be the other instance
            await Assert.That(otherInstance).IsEqualTo(instance);
            await Assert.That(otherInstance).IsNotSameReferenceAs(instance);
        }

        public class Subject : AggregateRoot
        {
            public Subject(string naturalKey)
            {
                this.Apply(new NewSubject { NaturalKey = naturalKey });
            }

            internal Subject()
            {
            }

            public string? NaturalKey { get; private set; }

            protected override object? GetState() => this.NaturalKey;

            protected override void SetState(object memento) => this.NaturalKey = memento.ToString();

            private void Handle(NewSubject @event) => this.NaturalKey = @event.NaturalKey;
        }

        public class NewSubject
        {
            public string? NaturalKey { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>()
                    .ToUseNaturalKey(subject => subject.NaturalKey)
                    .ToReconstituteUsing(() => new Subject());
            }
        }
    }
}
