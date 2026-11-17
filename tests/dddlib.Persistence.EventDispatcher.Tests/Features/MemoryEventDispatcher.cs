using dddlib.Configuration;
using dddlib.Persistence.Memory;
using dddlib.Persistence.Sdk;
using dddlib.Tests.Support;
using TUnit.Assertions.Enums;

namespace dddlib.Persistence.EventDispatcher.Tests.Features;

// As someone who uses dddlib with event sourcing
// In order to react to persisted events
// I need the in-memory event dispatcher to deliver the events an aggregate root has applied
public abstract class MemoryEventDispatcher : Feature
{
    protected MemoryEventDispatcher()
    {
        this.EventStore = new MemoryEventStore();
        this.Repository = new EventStoreRepository(new MemoryIdentityMap(), this.EventStore, new MemorySnapshotStore());
    }

    protected MemoryEventStore EventStore { get; }

    protected IEventStoreRepository Repository { get; }

    public sealed class CanDispatch : MemoryEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a memory event dispatcher over the repository's event store
            var dispatched = new TaskCompletionSource<NewSubject>();
            await using var eventDispatcher = new Memory.MemoryEventDispatcher(
                this.EventStore,
                (sequenceNumber, @event) => dispatched.TrySetResult((NewSubject)@event),
                new EventDispatcherOptions { PollingInterval = TimeSpan.FromMilliseconds(50) });
            eventDispatcher.Start();

            // And an instance of an aggregate root
            var instance = new Subject("key");

            // When that instance is saved to the repository
            await this.Repository.SaveAsync(instance);

            // Then the event is dispatched within a short period of time
            var newSubject = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(newSubject.Id).IsEqualTo(instance.Id);
        }

        public class Subject : AggregateRoot
        {
            public Subject(string id)
            {
                this.Apply(new NewSubject { Id = id });
            }

            internal Subject()
            {
            }

            [NaturalKey]
            public string? Id { get; private set; }

            private void Handle(NewSubject @event) => this.Id = @event.Id;
        }

        public class NewSubject
        {
            public string? Id { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    // The memento repository appends the events it is given to the aggregate root's stream, so they are dispatched too.
    public sealed class CanDispatchFromMementoRepository : MemoryEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a memory event dispatcher over the event store
            var dispatched = new TaskCompletionSource<NewSubject>();
            await using var eventDispatcher = new Memory.MemoryEventDispatcher(
                this.EventStore,
                (sequenceNumber, @event) => dispatched.TrySetResult((NewSubject)@event),
                new EventDispatcherOptions { PollingInterval = TimeSpan.FromMilliseconds(50) });
            eventDispatcher.Start();

            // And a memento repository composed with the same event store
            var repository = new MemoryRepository<Subject>(new MemoryIdentityMap(), this.EventStore);

            // And an instance of an aggregate root
            var instance = new Subject("key");

            // When that instance is saved to the memento repository
            await repository.SaveAsync(instance);

            // Then the event is dispatched within a short period of time
            var newSubject = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(newSubject.Id).IsEqualTo(instance.Id);
        }

        public class Subject : AggregateRoot
        {
            public Subject(string id)
            {
                this.Apply(new NewSubject { Id = id });
            }

            internal Subject()
            {
            }

            [NaturalKey]
            public string? Id { get; private set; }

            protected override object? GetState() => this.Id;

            protected override void SetState(object memento) => this.Id = memento.ToString();

            private void Handle(NewSubject @event) => this.Id = @event.Id;
        }

        public class NewSubject
        {
            public string? Id { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    // A positional record has no parameterless constructor; it is read back through its primary constructor
    // (https://github.com/dddlib/dddlib/issues/48).
    public sealed class CanDispatchPositionalRecordEvent : MemoryEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a memory event dispatcher over the repository's event store
            var dispatched = new TaskCompletionSource<object>();
            await using var eventDispatcher = new Memory.MemoryEventDispatcher(
                this.EventStore,
                (sequenceNumber, @event) => dispatched.TrySetResult(@event),
                new EventDispatcherOptions { PollingInterval = TimeSpan.FromMilliseconds(50) });
            eventDispatcher.Start();

            // And an instance of an aggregate root that applies a positional record
            var instance = new Subject("key");

            // When that instance is saved to the repository
            await this.Repository.SaveAsync(instance);

            // Then the event is dispatched within a short period of time
            var newSubject = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(newSubject).IsEqualTo(new NewSubject("key"));
        }

        public class Subject : AggregateRoot
        {
            public Subject(string id)
            {
                this.Apply(new NewSubject(id));
            }

            internal Subject()
            {
            }

            [NaturalKey]
            public string? Id { get; private set; }

            private void Handle(NewSubject @event) => this.Id = @event.Id;
        }

        public record NewSubject(string Id);

        private sealed class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed class CanDispatchFromAnEventStoreBatch : MemoryEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given an event dispatcher
            var dispatched = new List<string>();
            var both = new TaskCompletionSource();
            await using var eventDispatcher = new Memory.MemoryEventDispatcher(
                this.EventStore,
                (sequenceNumber, @event) =>
                {
                    lock (dispatched)
                    {
                        dispatched.Add(((NewSubject)@event).Id!);
                        if (dispatched.Count == 2)
                        {
                            both.TrySetResult();
                        }
                    }
                },
                new EventDispatcherOptions { PollingInterval = TimeSpan.FromMilliseconds(50) });
            eventDispatcher.Start();

            // When two instances of an aggregate root are saved together to the event store repository
            await this.Repository.SaveAsync([new Subject("first"), new Subject("second")]);

            // Then both events are dispatched, in the order of the instances
            await both.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(dispatched).IsEquivalentTo(["first", "second"], CollectionOrdering.Matching);
        }

        public class Subject : AggregateRoot
        {
            public Subject(string id)
            {
                this.Apply(new NewSubject { Id = id });
            }

            internal Subject()
            {
            }

            [NaturalKey]
            public string? Id { get; private set; }

            protected override object? GetState() => this.Id;

            protected override void SetState(object memento) => this.Id = memento.ToString();

            private void Handle(NewSubject @event) => this.Id = @event.Id;
        }

        public class NewSubject
        {
            public string? Id { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed class CanDispatchFromAMementoBatch : MemoryEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given an event dispatcher
            var dispatched = new List<object>();
            var both = new TaskCompletionSource();
            await using var eventDispatcher = new Memory.MemoryEventDispatcher(
                this.EventStore,
                (sequenceNumber, @event) =>
                {
                    lock (dispatched)
                    {
                        dispatched.Add(@event);
                        if (dispatched.Count == 2)
                        {
                            both.TrySetResult();
                        }
                    }
                },
                new EventDispatcherOptions { PollingInterval = TimeSpan.FromMilliseconds(50) });
            eventDispatcher.Start();

            // And memento repositories for two types of aggregate root
            var subjects = new MemoryRepository<Subject>(new MemoryIdentityMap(), this.EventStore);
            var others = new MemoryRepository<Other>(new MemoryIdentityMap(), this.EventStore);

            // When a new instance of each is saved in one batch
            var batch = new MemoryBatch();
            subjects.Add(batch, new Subject("subject"));
            others.Add(batch, new Other(7));
            await batch.SaveAsync();

            // Then both events are dispatched, in the order the instances were added
            await both.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(((NewSubject)dispatched[0]).Id).IsEqualTo("subject");
            await Assert.That(((NewOther)dispatched[1]).Number).IsEqualTo(7);
        }

        public class Subject : AggregateRoot
        {
            public Subject(string id)
            {
                this.Apply(new NewSubject { Id = id });
            }

            internal Subject()
            {
            }

            [NaturalKey]
            public string? Id { get; private set; }

            protected override object? GetState() => this.Id;

            protected override void SetState(object memento) => this.Id = memento.ToString();

            private void Handle(NewSubject @event) => this.Id = @event.Id;
        }

        public class NewSubject
        {
            public string? Id { get; set; }
        }

        public class Other : AggregateRoot
        {
            public Other(int number)
            {
                this.Apply(new NewOther { Number = number });
            }

            internal Other()
            {
            }

            [NaturalKey]
            public int Number { get; private set; }

            protected override object? GetState() => this.Number;

            protected override void SetState(object memento) => this.Number = (int)memento;

            private void Handle(NewOther @event) => this.Number = @event.Number;
        }

        public class NewOther
        {
            public int Number { get; set; }
        }

        private sealed class SubjectBootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }

        private sealed class OtherBootStrapper : IBootstrap<Other>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Other>().ToReconstituteUsing(() => new Other());
            }
        }
    }
}
