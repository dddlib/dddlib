using dddlib.Configuration;
using dddlib.Persistence.Memory;
using dddlib.Persistence.Sdk;
using dddlib.TestFramework;
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

    // Metadata the events do not carry, such as a tenant, and metadata of each event's own, such as a message ID, come
    // from the repository, alone or in a batch, as they do on SQL Server, and reach the feed beside dddlib's own
    // (https://github.com/dddlib/dddlib/issues/60).
    public sealed class EventMetadataReachesTheFeed : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository that gives its events a tenant, and each event a message ID made of its aggregate
            // root's identity and its position
            var eventStore = new MemoryEventStore();
            var repository = new MessageIdRepository(eventStore);

            // When an instance with two events is saved alone
            var alone = new EventsAreStoredForDispatch.Subject("alone");
            alone.Rename("renamed");
            await repository.SaveAsync(alone);

            // And two instances are saved in a batch
            var batch = new MemoryBatch();
            repository.Add(batch, new EventsAreStoredForDispatch.Subject("first"));
            repository.Add(batch, new EventsAreStoredForDispatch.Subject("second"));
            await batch.SaveAsync();

            // Then every event in the feed carries dddlib's metadata, the tenant and its own message ID
            var events = (await eventStore.ReadEventsAsync(0, 10)).Events;
            await Assert.That(events).Count().IsEqualTo(4);
            foreach (var @event in events)
            {
                await Assert.That(@event.Metadata.Keys).IsEquivalentTo(["hostname", "timestamp", "tenant", "message_id"]);
                await Assert.That(@event.Metadata["hostname"]).IsEqualTo(Environment.MachineName);
                await Assert.That(@event.Metadata["tenant"]).IsEqualTo("acme");
                await Assert.That(@event.Metadata["message_id"]).IsEqualTo($"{@event.StreamId:N}/{@event.StreamRevision - 1}");
            }

            // And the events of one save share a timestamp
            await Assert.That(events[0].Metadata["timestamp"]).IsEqualTo(events[1].Metadata["timestamp"]);
        }

        public sealed class MessageIdRepository(MemoryEventStore eventStore) : MemoryRepository<EventsAreStoredForDispatch.Subject>(new MemoryIdentityMap(), eventStore)
        {
            protected override IReadOnlyDictionary<string, string>? EventMetadata => new Dictionary<string, string> { ["tenant"] = "acme" };

            protected override IReadOnlyDictionary<string, string>? GetEventMetadata(Guid id, object @event, int index) =>
                new Dictionary<string, string> { ["message_id"] = $"{id:N}/{index}" };
        }
    }

    // An event's own metadata replaces the repository's entry with the same key, for that event only.
    public sealed class EventMetadataOverridesTheSavesMetadata : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository that gives its events a tenant, and its second event of a save another tenant
            var eventStore = new MemoryEventStore();
            var repository = new OverridingRepository(eventStore);

            // When an instance with two events is saved
            var instance = new EventsAreStoredForDispatch.Subject("key");
            instance.Rename("renamed");
            await repository.SaveAsync(instance);

            // Then only the second event carries the other tenant
            var events = (await eventStore.ReadEventsAsync(0, 10)).Events;
            await Assert.That(events[0].Metadata["tenant"]).IsEqualTo("acme");
            await Assert.That(events[1].Metadata["tenant"]).IsEqualTo("other");
        }

        public sealed class OverridingRepository(MemoryEventStore eventStore) : MemoryRepository<EventsAreStoredForDispatch.Subject>(new MemoryIdentityMap(), eventStore)
        {
            protected override IReadOnlyDictionary<string, string>? EventMetadata => new Dictionary<string, string> { ["tenant"] = "acme" };

            protected override IReadOnlyDictionary<string, string>? GetEventMetadata(Guid id, object @event, int index) =>
                index == 1 ? new Dictionary<string, string> { ["tenant"] = "other" } : null;
        }
    }

    // dddlib's hostname and timestamp cannot be replaced, by the repository's metadata or an event's own, and nothing is
    // stored when either tries.
    public sealed class EventMetadataCannotReplaceDddlibsOwn : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository that gives its events a hostname, and one that gives each event a timestamp
            var eventStore = new MemoryEventStore();
            var hostname = new ReservedRepository(eventStore, new Dictionary<string, string> { ["hostname"] = "web-01" }, null);
            var timestamp = new ReservedRepository(eventStore, null, new Dictionary<string, string> { ["timestamp"] = "2026-01-01T00:00:00Z" });

            // When an instance is saved through each, alone and in a batch
            // Then each save is rejected, naming the key
            await Assert.That(() => hostname.SaveAsync(new EventsAreStoredForDispatch.Subject("a")))
                .Throws<ArgumentException>()
                .WithMessageContaining("The metadata key 'hostname' is reserved for dddlib.");
            await Assert.That(() => timestamp.SaveAsync(new EventsAreStoredForDispatch.Subject("b")))
                .Throws<ArgumentException>()
                .WithMessageContaining("The metadata key 'timestamp' is reserved for dddlib.");

            var batch = new MemoryBatch();
            hostname.Add(batch, new EventsAreStoredForDispatch.Subject("c"));
            await Assert.That(() => batch.SaveAsync())
                .Throws<ArgumentException>()
                .WithMessageContaining("The metadata key 'hostname' is reserved for dddlib.");

            // And nothing is stored
            await Assert.That(await eventStore.GetLastSequenceNumberAsync()).IsEqualTo(0);
            await Assert.That(async () => await hostname.LoadAsync("a")).Throws<AggregateRootNotFoundException>();
        }

        public sealed class ReservedRepository(
            MemoryEventStore eventStore,
            IReadOnlyDictionary<string, string>? metadata,
            IReadOnlyDictionary<string, string>? eventMetadata) : MemoryRepository<EventsAreStoredForDispatch.Subject>(new MemoryIdentityMap(), eventStore)
        {
            protected override IReadOnlyDictionary<string, string>? EventMetadata => metadata;

            protected override IReadOnlyDictionary<string, string>? GetEventMetadata(Guid id, object @event, int index) => eventMetadata;
        }
    }

    // A batch saves aggregate roots of different types, each through its own repository, in one transaction. Here
    // each repository has its own identity map and event store.
    public sealed class BatchSavesRootsOfDifferentTypes : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given repositories for two types of aggregate root
            var subjects = new MemoryRepository<Subject>();
            var others = new MemoryRepository<Other>();

            // And a new instance of each
            var subject = new Subject("subject");
            var other = new Other(7);

            // When both instances are added to a batch and the batch is saved
            var batch = new MemoryBatch();
            subjects.Add(batch, subject);
            others.Add(batch, other);
            await batch.SaveAsync();

            // Then each instance loads as it was saved
            await Assert.That(await subjects.LoadAsync("subject")).IsEqualTo(subject);
            await Assert.That(await others.LoadAsync(7)).IsEqualTo(other);

            // And the saved instances have no uncommitted events
            await Assert.That(subject.GetUncommittedEvents()).IsEmpty();
            await Assert.That(other.GetUncommittedEvents()).IsEmpty();

            // And a saved instance can be changed and saved again on its own
            subject.Rename("renamed");
            await subjects.SaveAsync(subject);
            await Assert.That((await subjects.LoadAsync("subject")).Name).IsEqualTo("renamed");
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

    public sealed class BatchIsAllOrNothing : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given repositories for two types of aggregate root, sharing an event store
            var eventStore = new MemoryEventStore();
            var subjects = new MemoryRepository<Subject>(new MemoryIdentityMap(), eventStore);
            var others = new MemoryRepository<Other>(new MemoryIdentityMap(), eventStore);

            // And an instance of an aggregate root that has been saved
            var stale = new Subject("stale");
            await subjects.SaveAsync(stale);

            // And another copy of it was loaded, changed and saved
            var winner = await subjects.LoadAsync("stale");
            winner.Rename("winner");
            await subjects.SaveAsync(winner);

            // And the first instance was changed too
            stale.Rename("loser");

            // And a new instance of another type
            var other = new Other(7);

            // When both instances are added to a batch and the batch is saved
            var batch = new MemoryBatch();
            others.Add(batch, other);
            subjects.Add(batch, stale);
            Func<Task> save = () => batch.SaveAsync();

            // Then a concurrency exception names the stale instance
            await Assert.That(save)
                .Throws<ConcurrencyException>()
                .WithMessageContaining($"'{typeof(Subject)}' with natural key 'stale'");

            // And the new instance was not saved
            await Assert.That(() => (Task)others.LoadAsync(7)).Throws<AggregateRootNotFoundException>();

            // And the stored instance has only the winning change
            await Assert.That((await subjects.LoadAsync("stale")).Name).IsEqualTo("winner");

            // And no events of the batch were stored
            await Assert.That((await eventStore.ReadEventsAsync(0, 10)).Events).Count().IsEqualTo(2);

            // And neither instance's events were committed
            await Assert.That(other.GetUncommittedEvents()).Count().IsEqualTo(1);
            await Assert.That(stale.GetUncommittedEvents()).Count().IsEqualTo(1);

            // And the new instance can still be saved on its own
            await others.SaveAsync(other);
            await Assert.That(await others.LoadAsync(7)).IsEqualTo(other);
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

    public sealed class BatchStoresTheEventsOfEveryRoot : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given repositories for two types of aggregate root, sharing an event store
            var eventStore = new MemoryEventStore();
            var identityMap = new MemoryIdentityMap();
            var subjects = new MemoryRepository<Subject>(identityMap, eventStore);
            var others = new MemoryRepository<Other>(identityMap, eventStore);

            // And a new instance of each
            var subject = new Subject("subject");
            var other = new Other(7);

            // When both instances are added to a batch and the batch is saved
            var batch = new MemoryBatch();
            subjects.Add(batch, subject);
            others.Add(batch, other);
            await batch.SaveAsync();

            // Then their events are stored in the order the instances were added, with contiguous sequence numbers
            var events = (await eventStore.ReadEventsAsync(0, 10)).Events;
            await Assert.That(events).Count().IsEqualTo(2);
            await Assert.That(events[0].Event).IsTypeOf<NewSubject>();
            await Assert.That(events[1].Event).IsTypeOf<NewOther>();
            await Assert.That(events[1].SequenceNumber).IsEqualTo(events[0].SequenceNumber + 1);

            // And they share one correlation id
            await Assert.That(events[1].CorrelationId).IsEqualTo(events[0].CorrelationId);

            // And each stream carries its memento's state token
            var subjectId = await identityMap.TryGetAsync(typeof(Subject), typeof(string), "subject");
            await Assert.That((await eventStore.GetStreamAsync(subjectId!.Value, 0)).State).IsEqualTo(subject.State);
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

            protected override object? GetState() => this.NaturalKey;

            protected override void SetState(object memento) => this.NaturalKey = memento.ToString();

            private void Handle(NewSubject @event) => this.NaturalKey = @event.NaturalKey;
        }

        public class NewSubject
        {
            public string? NaturalKey { get; set; }
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

    public sealed class BatchIsSingleUse : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository
            var repository = new MemoryRepository<Subject>();

            // And a batch that has been saved
            var batch = new MemoryBatch();
            repository.Add(batch, new Subject("first"));
            await batch.SaveAsync();

            // When an instance is added to that batch
            // Then the batch refuses it
            await Assert.That(() => repository.Add(batch, new Subject("second"))).Throws<InvalidOperationException>();

            // And the batch cannot be saved again
            await Assert.That(() => batch.SaveAsync()).Throws<InvalidOperationException>();

            // And a batch that failed cannot be saved again either
            var failed = new MemoryBatch();
            repository.Add(failed, new Subject("first"));
            await Assert.That(() => failed.SaveAsync()).Throws<ConcurrencyException>();
            await Assert.That(() => failed.SaveAsync()).Throws<InvalidOperationException>();
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
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed class EmptyBatchDoesNothing : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository and an empty batch
            var repository = new MemoryRepository<Subject>();
            var batch = new MemoryBatch();

            // When the batch is saved
            await batch.SaveAsync();

            // Then nothing was saved
            await Assert.That(() => (Task)repository.LoadAsync("anything")).Throws<AggregateRootNotFoundException>();

            // And adding nothing, or to nothing, is rejected
            await Assert.That(() => repository.Add(new MemoryBatch(), null!)).Throws<ArgumentNullException>();
            await Assert.That(() => repository.Add(null!, new Subject("key"))).Throws<ArgumentNullException>();
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
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed class BatchRejectsTheSameRootTwice : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository and a batch holding an instance of an aggregate root
            var repository = new MemoryRepository<Subject>();
            var subject = new Subject("twice");
            var batch = new MemoryBatch();
            repository.Add(batch, subject);

            // When that instance is added to the batch again
            // Then the batch refuses it
            await Assert.That(() => repository.Add(batch, subject)).Throws<ArgumentException>();

            // And the batch still saves the instance once
            await batch.SaveAsync();
            await Assert.That(await repository.LoadAsync("twice")).IsEqualTo(subject);
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
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed class BatchRejectsTwoRootsWithOneNaturalKey : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository and two new instances of an aggregate root with the same natural key
            var repository = new MemoryRepository<Subject>();
            var first = new Subject("same");
            var second = new Subject("same");

            // When both instances are added to a batch and the batch is saved
            var batch = new MemoryBatch();
            repository.Add(batch, first);
            repository.Add(batch, second);
            Func<Task> save = () => batch.SaveAsync();

            // Then a concurrency exception names the natural key
            await Assert.That(save)
                .Throws<ConcurrencyException>()
                .WithMessageContaining($"'{typeof(Subject)}' with natural key 'same'");

            // And nothing was saved
            await Assert.That(() => (Task)repository.LoadAsync("same")).Throws<AggregateRootNotFoundException>();
            await Assert.That(first.GetUncommittedEvents()).Count().IsEqualTo(1);
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
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed class BatchRejectsARepositoryOfAnotherKind : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a memory repository and a batch of another kind
            var repository = new MemoryRepository<Subject>();
            var foreign = new ForeignBatch();

            // When an instance is added to that batch
            // Then the repository refuses it
            await Assert.That(() => repository.Add(foreign, new Subject("key"))).Throws<ArgumentException>();

            // Given a repository that cannot take part in batches and a memory batch
            var custom = new CustomRepository();
            var batch = new MemoryBatch();

            // When an instance is added to the batch through that repository
            // Then the repository refuses it, with advice
            await Assert.That(() => custom.Add(batch, new Subject("key")))
                .Throws<NotSupportedException>()
                .WithMessageContaining("To fix this issue");
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
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }

        private sealed class ForeignBatch : IBatch
        {
            public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task SaveAsync(Guid correlationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private sealed class CustomRepository() : Repository<Subject>(new MemoryIdentityMap())
        {
            protected override Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, Guid correlationId, string? preCommitState, CancellationToken cancellationToken) =>
                Task.FromResult("state");

            protected override Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
                Task.FromResult<MementoResult?>(null);
        }
    }

    // The caller supplies the correlation id of a save or of a batch, for every event it commits, as it does to the event
    // store repository; without one each commit gets a new one (https://github.com/dddlib/dddlib/issues/63).
    public sealed class CallerSuppliesTheCorrelationId : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository
            var eventStore = new MemoryEventStore();
            var repository = new MemoryRepository<EventsAreStoredForDispatch.Subject>(new MemoryIdentityMap(), eventStore);

            // When an instance with two events is saved with a correlation id
            var correlationId = Guid.NewGuid();
            var alone = new EventsAreStoredForDispatch.Subject("alone");
            alone.Rename("renamed");
            await repository.SaveAsync(alone, correlationId);

            // And two instances are saved in a batch with another correlation id
            var batchCorrelationId = Guid.NewGuid();
            var batch = new MemoryBatch();
            repository.Add(batch, new EventsAreStoredForDispatch.Subject("first"));
            repository.Add(batch, new EventsAreStoredForDispatch.Subject("second"));
            await batch.SaveAsync(batchCorrelationId);

            // And an instance is saved alone, and another in a batch, without a correlation id
            await repository.SaveAsync(new EventsAreStoredForDispatch.Subject("unsupplied"));
            var unsupplied = new MemoryBatch();
            repository.Add(unsupplied, new EventsAreStoredForDispatch.Subject("unsupplied batch"));
            await unsupplied.SaveAsync();

            // Then the events of each supplied commit carry its correlation id
            var events = (await eventStore.ReadEventsAsync(0, 10)).Events;
            await Assert.That(events).Count().IsEqualTo(6);
            await Assert.That(events[0].CorrelationId).IsEqualTo(correlationId);
            await Assert.That(events[1].CorrelationId).IsEqualTo(correlationId);
            await Assert.That(events[2].CorrelationId).IsEqualTo(batchCorrelationId);
            await Assert.That(events[3].CorrelationId).IsEqualTo(batchCorrelationId);

            // And each commit without one has a new correlation id of its own
            await Assert.That(events.Select(static @event => @event.CorrelationId).Distinct().Count()).IsEqualTo(4);
            await Assert.That(events[4].CorrelationId).IsNotEqualTo(Guid.Empty);
            await Assert.That(events[5].CorrelationId).IsNotEqualTo(Guid.Empty);
        }
    }

    // An empty correlation id is refused before anything is stored, and the batch it was refused for can still be saved.
    public sealed class EmptyCorrelationIdIsRefused : MemoryMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository and an instance of an aggregate root
            var eventStore = new MemoryEventStore();
            var repository = new MemoryRepository<EventsAreStoredForDispatch.Subject>(new MemoryIdentityMap(), eventStore);
            var instance = new EventsAreStoredForDispatch.Subject("key");

            // When the instance is saved with an empty correlation id
            // Then the save is refused
            await Assert.That(() => repository.SaveAsync(instance, Guid.Empty)).Throws<ArgumentException>();

            // And nothing was stored
            await Assert.That(() => (Task)repository.LoadAsync("key")).Throws<AggregateRootNotFoundException>();
            await Assert.That(instance.GetUncommittedEvents()).Count().IsEqualTo(1);

            // When the instance is added to a batch that is saved with an empty correlation id
            var batch = new MemoryBatch();
            repository.Add(batch, instance);

            // Then the save is refused
            await Assert.That(() => batch.SaveAsync(Guid.Empty)).Throws<ArgumentException>();
            await Assert.That((await eventStore.ReadEventsAsync(0, 10)).Events).IsEmpty();

            // And the batch can still be saved with a correlation id
            var correlationId = Guid.NewGuid();
            await batch.SaveAsync(correlationId);
            await Assert.That((await eventStore.ReadEventsAsync(0, 10)).Events.Single().CorrelationId).IsEqualTo(correlationId);
        }
    }
}
