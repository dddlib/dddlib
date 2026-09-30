using dddlib.Configuration;
using dddlib.Persistence.Memory;
using dddlib.Persistence.Sdk;
using dddlib.Tests.Support;

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
}
