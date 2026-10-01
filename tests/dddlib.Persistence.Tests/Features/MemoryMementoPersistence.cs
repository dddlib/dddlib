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

    // Events applied to the aggregate root are appended to its stream when the memento is saved, so that they can be
    // dispatched. The memento remains the source of state; the events are not used for reconstitution.
    public sealed class EventsAreStoredForDispatch : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an event store
            var eventStore = new MemoryEventStore();

            // And a repository composed with that event store
            var identityMap = new MemoryIdentityMap();
            var repository = new MemoryRepository<Subject>(identityMap, eventStore);

            // And an instance of an aggregate root with a natural key
            var instance = new Subject("key");

            // When that instance is saved to the repository
            await repository.SaveAsync(instance);

            // Then its event is in the event store and the instance has no uncommitted events
            var events = (await eventStore.ReadEventsAsync(0, 10)).Events;
            await Assert.That(events).Count().IsEqualTo(1);
            await Assert.That(((NewSubject)events[0].Event).NaturalKey).IsEqualTo("key");
            await Assert.That(instance.GetUncommittedEvents()).IsEmpty();

            // When the instance is loaded, changed and saved again
            var loaded = await repository.LoadAsync("key");
            loaded.Rename("name");
            await repository.SaveAsync(loaded);

            // Then the stream has both events and carries the memento's state token
            var id = await identityMap.TryGetAsync(typeof(Subject), typeof(string), "key");
            var stream = await eventStore.GetStreamAsync(id!.Value, 0);
            await Assert.That(stream.Events).Count().IsEqualTo(2);
            await Assert.That(((SubjectRenamed)stream.Events[1]).Name).IsEqualTo("name");
            await Assert.That(stream.State).IsEqualTo(loaded.State);

            // When the instance is saved again without changes
            await repository.SaveAsync(loaded);

            // Then nothing more is appended
            await Assert.That((await eventStore.ReadEventsAsync(0, 10)).Events).Count().IsEqualTo(2);
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

            [NaturalKey]
            public string? NaturalKey { get; private set; }

            public string? Name { get; private set; }

            public void Rename(string name) => this.Apply(new SubjectRenamed { Name = name });

            protected override object? GetState() => new Memento { NaturalKey = this.NaturalKey, Name = this.Name };

            protected override void SetState(object memento)
            {
                var subject = (Memento)memento;
                this.NaturalKey = subject.NaturalKey;
                this.Name = subject.Name;
            }

            private void Handle(NewSubject @event) => this.NaturalKey = @event.NaturalKey;

            private void Handle(SubjectRenamed @event) => this.Name = @event.Name;

            public sealed class Memento
            {
                public string? NaturalKey { get; set; }

                public string? Name { get; set; }
            }
        }

        public class NewSubject
        {
            public string? NaturalKey { get; set; }
        }

        public class SubjectRenamed
        {
            public string? Name { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }
}
