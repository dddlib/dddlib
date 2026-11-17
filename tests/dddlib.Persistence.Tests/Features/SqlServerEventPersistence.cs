using dddlib.Configuration;
using dddlib.Persistence.SqlServer;
using dddlib.Persistence.Sdk;
using dddlib.TestFramework;
using dddlib.Tests.Support;
using TUnit.Assertions.Enums;

namespace dddlib.Persistence.Tests.Features;

// As someone who uses dddlib with event sourcing
// In order to persist aggregate roots durably
// I need the SQL Server event store repository to save and load aggregate roots
public abstract class SqlServerEventPersistence : SqlServerFeature
{
    protected IIdentityMap IdentityMap { get; private set; } = null!;

    protected IEventStore EventStore { get; private set; } = null!;

    protected ISnapshotStore SnapshotStore { get; private set; } = null!;

    protected IEventStoreRepository Repository { get; private set; } = null!;

    [Before(Test)]
    public void CreateStores()
    {
        // Given an identity map, an event store, a snapshot store and an event store repository
        this.IdentityMap = new SqlServerIdentityMap(this.ConnectionString);
        this.EventStore = new SqlServerEventStore(this.ConnectionString);
        this.SnapshotStore = new SqlServerSnapshotStore(this.ConnectionString);
        this.Repository = new EventStoreRepository(this.IdentityMap, this.EventStore, this.SnapshotStore);
    }

    public sealed class UndefinedNaturalKey : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root with no defined natural key
            var instance = new Subject();

            // When that instance is saved to the repository
            var action = () => this.Repository.SaveAsync(instance);

            // Then a persistence exception is thrown
            await Assert.That(action).Throws<PersistenceException>();
        }

        public class Subject : AggregateRoot
        {
        }

        private sealed class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed class UndefinedUnititializedFactory : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root with no defined uninitialized factory
            var instance = new Subject("nonsense");

            // When that instance is saved to the repository
            var action = () => this.Repository.SaveAsync(instance);

            // Then a persistence exception is thrown
            await Assert.That(action).Throws<PersistenceException>();
        }

        public class Subject : AggregateRoot
        {
            public Subject(string nonsense)
            {
                _ = nonsense;
            }

            [NaturalKey]
            public string? Id { get; set; }
        }
    }

    public sealed class NullNaturalKey : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root with a null natural key
            var instance = new Subject();

            // When that instance is saved to the repository
            var action = () => this.Repository.SaveAsync(instance);

            // Then an argument exception is thrown
            await Assert.That(action).Throws<ArgumentException>();
        }

        public class Subject : AggregateRoot
        {
            [NaturalKey]
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

    public sealed class SaveAndLoad : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root
            var saved = new Subject("test");

            // And that instance is saved to the repository
            await this.Repository.SaveAsync(saved);

            // When that instance is loaded from the repository
            var loaded = await this.Repository.LoadAsync<Subject>(saved.Id!);

            // Then the loaded instance should be the saved instance
            await Assert.That(loaded).IsEqualTo(saved);

            // And their revisions should be equal
            await Assert.That(loaded.GetRevision()).IsEqualTo(saved.GetRevision());

            // And their mementos should match
            await Assert.That(MementoJson.Of(loaded.GetMemento())).IsEqualTo(MementoJson.Of(saved.GetMemento()));
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

    public sealed class SaveAndSaveAndLoad : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root
            var saved = new Subject("test2");

            // And that instance is saved to the repository
            await this.Repository.SaveAsync(saved);

            // And something happened to that instance
            saved.DoSomething();

            // And that instance is saved again to the repository
            await this.Repository.SaveAsync(saved);

            // When that instance is loaded from the repository
            var loaded = await this.Repository.LoadAsync<Subject>(saved.Id!);

            // Then the loaded instance should be the saved instance
            await Assert.That(loaded).IsEqualTo(saved);

            // And their revisions should be equal
            await Assert.That(loaded.GetRevision()).IsEqualTo(saved.GetRevision());

            // And their mementos should match
            await Assert.That(MementoJson.Of(loaded.GetMemento())).IsEqualTo(MementoJson.Of(saved.GetMemento()));
        }

        public class Subject : AggregateRoot
        {
            private bool hasDoneSomething;

            public Subject(string id)
            {
                this.Apply(new NewSubject { Id = id });
            }

            internal Subject()
            {
            }

            [NaturalKey]
            public string? Id { get; private set; }

            public void DoSomething() => this.Apply(new SubjectDidSomething { Id = this.Id });

            protected override object? GetState() => new Memento { Id = this.Id, HasDoneSomething = this.hasDoneSomething };

            protected override void SetState(object memento)
            {
                var subject = (Memento)memento;
                this.Id = subject.Id;
                this.hasDoneSomething = subject.HasDoneSomething;
            }

            private void Handle(NewSubject @event) => this.Id = @event.Id;

            private void Handle(SubjectDidSomething @event) => this.hasDoneSomething = true;

            private sealed class Memento
            {
                public string? Id { get; set; }

                public bool HasDoneSomething { get; set; }
            }
        }

        public class NewSubject
        {
            public string? Id { get; set; }
        }

        public class SubjectDidSomething
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

    public sealed class SaveAndLoadAndSaveAndLoad : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root
            var saved = new Subject("test3");

            // And that instance is saved to the repository
            await this.Repository.SaveAsync(saved);

            // And that instance is loaded from the repository
            var loaded = await this.Repository.LoadAsync<Subject>(saved.Id!);

            // And something happened to that loaded instance
            loaded.DoSomething();

            // And that loaded instance is saved to the repository
            await this.Repository.SaveAsync(loaded);

            // When another instance is loaded from the repository
            var anotherLoaded = await this.Repository.LoadAsync<Subject>(saved.Id!);

            // Then the other loaded instance should be the loaded instance
            await Assert.That(anotherLoaded).IsEqualTo(loaded);

            // And their revisions should be equal
            await Assert.That(anotherLoaded.GetRevision()).IsEqualTo(loaded.GetRevision());

            // And their mementos should match
            await Assert.That(MementoJson.Of(anotherLoaded.GetMemento())).IsEqualTo(MementoJson.Of(loaded.GetMemento()));
        }

        public class Subject : AggregateRoot
        {
            private bool hasDoneSomething;

            public Subject(string id)
            {
                this.Apply(new NewSubject { Id = id });
            }

            internal Subject()
            {
            }

            [NaturalKey]
            public string? Id { get; private set; }

            public void DoSomething() => this.Apply(new SubjectDidSomething { Id = this.Id });

            protected override object? GetState() => new Memento { Id = this.Id, HasDoneSomething = this.hasDoneSomething };

            protected override void SetState(object memento)
            {
                var subject = (Memento)memento;
                this.Id = subject.Id;
                this.hasDoneSomething = subject.HasDoneSomething;
            }

            private void Handle(NewSubject @event) => this.Id = @event.Id;

            private void Handle(SubjectDidSomething @event) => this.hasDoneSomething = true;

            private sealed class Memento
            {
                public string? Id { get; set; }

                public bool HasDoneSomething { get; set; }
            }
        }

        public class NewSubject
        {
            public string? Id { get; set; }
        }

        public class SubjectDidSomething
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

    public sealed class SnapshotAndLoad : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root
            var saved = new Subject("test4");

            // And that instance is saved to the repository
            await this.Repository.SaveAsync(saved);

            // And that instance is snapshot to the repository
            var streamId = await this.IdentityMap.TryGetAsync(typeof(Subject), typeof(string), saved.Id!);
            await this.SnapshotStore.PutSnapshotAsync(streamId!.Value, new Snapshot(saved.GetRevision(), saved.GetMemento()));

            // When that instance is loaded from the repository
            var loaded = await this.Repository.LoadAsync<Subject>(saved.Id!);

            // Then the loaded instance should be the saved instance
            await Assert.That(loaded).IsEqualTo(saved);

            // And their revisions should be equal
            await Assert.That(loaded.GetRevision()).IsEqualTo(saved.GetRevision());

            // And their mementos should match
            await Assert.That(MementoJson.Of(loaded.GetMemento())).IsEqualTo(MementoJson.Of(saved.GetMemento()));
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

    public sealed class SnapshotAndSaveAndLoad : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root
            var saved = new Subject("test5");

            // And that instance is saved to the repository
            await this.Repository.SaveAsync(saved);

            // And that instance is snapshot to the repository
            var streamId = (await this.IdentityMap.TryGetAsync(typeof(Subject), typeof(string), saved.Id!))!.Value;
            await this.SnapshotStore.PutSnapshotAsync(streamId, new Snapshot(saved.GetRevision(), saved.GetMemento()));

            // And something happened to that instance
            saved.DoSomething();

            // And that instance is saved again to the repository
            await this.Repository.SaveAsync(saved);

            // When that instance is loaded from the repository
            var loaded = await this.Repository.LoadAsync<Subject>(saved.Id!);

            // And the events for that instance are loaded from the event store
            var stream = await this.EventStore.GetStreamAsync(streamId, 0);

            // Then the loaded instance should be the saved instance
            await Assert.That(loaded).IsEqualTo(saved);

            // And their revisions should be equal
            await Assert.That(loaded.GetRevision()).IsEqualTo(saved.GetRevision());

            // And their mementos should match
            await Assert.That(MementoJson.Of(loaded.GetMemento())).IsEqualTo(MementoJson.Of(saved.GetMemento()));

            // And the loaded events should contain two matching events
            await Assert.That(stream.Events).Count().IsEqualTo(2);
            await Assert.That(stream.Events[0]).IsTypeOf<NewSubject>();
            await Assert.That(((NewSubject)stream.Events[0]).Id).IsEqualTo(saved.Id);
            await Assert.That(stream.Events[1]).IsTypeOf<SubjectDidSomething>();
            await Assert.That(((SubjectDidSomething)stream.Events[1]).Id).IsEqualTo(saved.Id);
        }

        public class Subject : AggregateRoot
        {
            private bool hasDoneSomething;

            public Subject(string id)
            {
                this.Apply(new NewSubject { Id = id });
            }

            internal Subject()
            {
            }

            [NaturalKey]
            public string? Id { get; private set; }

            public void DoSomething() => this.Apply(new SubjectDidSomething { Id = this.Id });

            protected override object? GetState() => new Memento { Id = this.Id, HasDoneSomething = this.hasDoneSomething };

            protected override void SetState(object memento)
            {
                var subject = (Memento)memento;
                this.Id = subject.Id;
                this.hasDoneSomething = subject.HasDoneSomething;
            }

            private void Handle(NewSubject @event) => this.Id = @event.Id;

            private void Handle(SubjectDidSomething @event) => this.hasDoneSomething = true;

            private sealed class Memento
            {
                public string? Id { get; set; }

                public bool HasDoneSomething { get; set; }
            }
        }

        public class NewSubject
        {
            public string? Id { get; set; }
        }

        public class SubjectDidSomething
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

    public sealed class SaveAndEndLifecycleAndSaveAndCreate : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a natural key value
            var naturalKey = "naturalKey";

            // And an instance of an aggregate root with that natural key
            var saved = new Subject(naturalKey);

            // And that instance is saved to the repository
            await this.Repository.SaveAsync(saved);

            // And that instance is loaded from the repository
            var loaded = await this.Repository.LoadAsync<Subject>(naturalKey);

            // And that instance is destroyed
            loaded.Destroy();

            // And no further operations can occur against that instance
            await Assert.That(loaded.Destroy).Throws<BusinessException>();

            // And that destroyed instance is saved to the repository
            await this.Repository.SaveAsync(loaded);

            // When a temporally new instance of an aggregate root with that same natural key is created
            var temporallyNew = new Subject(naturalKey);

            // And that temporally new instance is saved to the repository
            var action = () => this.Repository.SaveAsync(temporallyNew);

            // Then the operation completes without an exception being thrown
            await Assert.That(action).ThrowsNothing();

            // And further operations can occur against that instance
            var actual = await this.Repository.LoadAsync<Subject>(naturalKey);
            await Assert.That(actual.Destroy).ThrowsNothing();
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

            public void Destroy() => this.Apply(new SubjectDestroyed { Id = this.Id });

            private void Handle(NewSubject @event) => this.Id = @event.Id;

            private void Handle(SubjectDestroyed @event) => this.EndLifecycle();
        }

        public class NewSubject
        {
            public string? Id { get; set; }
        }

        public class SubjectDestroyed
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
    public sealed class SaveAndLoadWithPositionalRecordEvents : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root that applies positional records
            var saved = new Subject("test6");

            // And something happened to that instance
            saved.Rename("renamed");

            // And that instance is saved to the repository
            await this.Repository.SaveAsync(saved);

            // When that instance is loaded from the repository
            var loaded = await this.Repository.LoadAsync<Subject>(saved.Id!);

            // And the events for that instance are loaded from the event store
            var streamId = (await this.IdentityMap.TryGetAsync(typeof(Subject), typeof(string), saved.Id!))!.Value;
            var stream = await this.EventStore.GetStreamAsync(streamId, 0);

            // Then the loaded instance should be the saved instance
            await Assert.That(loaded).IsEqualTo(saved);

            // And their revisions should be equal
            await Assert.That(loaded.GetRevision()).IsEqualTo(saved.GetRevision());

            // And the state applied by the events should match
            await Assert.That(loaded.Name).IsEqualTo(saved.Name);

            // And the loaded events should equal the applied events
            await Assert.That(stream.Events).Count().IsEqualTo(2);
            await Assert.That(stream.Events[0]).IsEqualTo(new NewSubject("test6"));
            await Assert.That(stream.Events[1]).IsEqualTo(new SubjectRenamed("test6", "renamed"));
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

            public string? Name { get; private set; }

            public void Rename(string name) => this.Apply(new SubjectRenamed(this.Id!, name));

            private void Handle(NewSubject @event) => this.Id = @event.Id;

            private void Handle(SubjectRenamed @event) => this.Name = @event.Name;
        }

        public record NewSubject(string Id);

        public record SubjectRenamed(string Id, string Name);

        private sealed class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed class SaveManyAndLoad : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given two new instances of an aggregate root
            var first = new Subject("first");
            var second = new Subject("second");

            // When both instances are saved to the repository together
            await this.Repository.SaveAsync([first, second]);

            // Then each instance loads as it was saved
            var loadedFirst = await this.Repository.LoadAsync<Subject>("first");
            var loadedSecond = await this.Repository.LoadAsync<Subject>("second");
            await Assert.That(loadedFirst).IsEqualTo(first);
            await Assert.That(loadedSecond).IsEqualTo(second);

            // And their revisions should be equal
            await Assert.That(loadedFirst.GetRevision()).IsEqualTo(first.GetRevision());
            await Assert.That(loadedSecond.GetRevision()).IsEqualTo(second.GetRevision());

            // And the saved instances have no uncommitted events
            await Assert.That(first.GetUncommittedEvents()).IsEmpty();
            await Assert.That(second.GetUncommittedEvents()).IsEmpty();
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

    public sealed class SaveManyOfDifferentTypes : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given new instances of two types of aggregate root
            var subject = new Subject("subject");
            var other = new Other(7);

            // When both instances are saved to the repository together
            await this.Repository.SaveAsync([subject, other]);

            // Then each instance loads as it was saved
            var loadedSubject = await this.Repository.LoadAsync<Subject>("subject");
            var loadedOther = await this.Repository.LoadAsync<Other>(7);
            await Assert.That(loadedSubject).IsEqualTo(subject);
            await Assert.That(loadedOther).IsEqualTo(other);
            await Assert.That(loadedSubject.GetRevision()).IsEqualTo(subject.GetRevision());
            await Assert.That(loadedOther.GetRevision()).IsEqualTo(other.GetRevision());
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

    public sealed class SaveManyMixesNewAndExistingRoots : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root that has been saved
            var existing = new Subject("existing");
            await this.Repository.SaveAsync(existing);

            // And something happened to that instance
            existing.DoSomething();

            // And a new instance
            var created = new Subject("created");

            // When both instances are saved to the repository together
            await this.Repository.SaveAsync([existing, created]);

            // Then the existing instance loads with the change
            var loadedExisting = await this.Repository.LoadAsync<Subject>("existing");
            await Assert.That(loadedExisting.Changes).IsEqualTo(1);
            await Assert.That(loadedExisting.GetRevision()).IsEqualTo(2);

            // And the new instance loads as it was saved
            var loadedCreated = await this.Repository.LoadAsync<Subject>("created");
            await Assert.That(loadedCreated.GetRevision()).IsEqualTo(1);

            // And the existing instance can be saved again
            existing.DoSomething();
            await this.Repository.SaveAsync(existing);
            await Assert.That((await this.Repository.LoadAsync<Subject>("existing")).Changes).IsEqualTo(2);
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

            public int Changes { get; private set; }

            public void DoSomething() => this.Apply(new SubjectDidSomething { Id = this.Id });

            private void Handle(NewSubject @event) => this.Id = @event.Id;

            private void Handle(SubjectDidSomething @event) => this.Changes++;
        }

        public class NewSubject
        {
            public string? Id { get; set; }
        }

        public class SubjectDidSomething
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

    public sealed class SaveManyIsAllOrNothing : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root that has been saved
            var stale = new Subject("stale");
            await this.Repository.SaveAsync(stale);

            // And another copy of it was loaded, changed and saved
            var winner = await this.Repository.LoadAsync<Subject>("stale");
            winner.DoSomething();
            await this.Repository.SaveAsync(winner);

            // And the first instance was changed too
            stale.DoSomething();

            // And a new instance
            var created = new Subject("created");

            // When both instances are saved to the repository together
            Func<Task> save = () => this.Repository.SaveAsync([created, stale]);

            // Then a concurrency exception names the stale instance
            await Assert.That(save)
                .Throws<ConcurrencyException>()
                .WithMessageContaining($"'{typeof(Subject)}' with natural key 'stale'");

            // And the new instance was not saved
            await Assert.That(() => (Task)this.Repository.LoadAsync<Subject>("created")).Throws<AggregateRootNotFoundException>();

            // And the stored instance has only the winning change
            var loaded = await this.Repository.LoadAsync<Subject>("stale");
            await Assert.That(loaded.Changes).IsEqualTo(1);
            await Assert.That(loaded.GetRevision()).IsEqualTo(2);

            // And neither instance's events were committed
            await Assert.That(created.GetUncommittedEvents()).Count().IsEqualTo(1);
            await Assert.That(stale.GetUncommittedEvents()).Count().IsEqualTo(1);

            // And the new instance can still be saved on its own
            await this.Repository.SaveAsync(created);
            await Assert.That(await this.Repository.LoadAsync<Subject>("created")).IsEqualTo(created);
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

            public int Changes { get; private set; }

            public void DoSomething() => this.Apply(new SubjectDidSomething { Id = this.Id });

            private void Handle(NewSubject @event) => this.Id = @event.Id;

            private void Handle(SubjectDidSomething @event) => this.Changes++;
        }

        public class NewSubject
        {
            public string? Id { get; set; }
        }

        public class SubjectDidSomething
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

    public sealed class SaveManySharesTheCorrelationId : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given two new instances of an aggregate root and a correlation id
            var first = new Subject("first");
            var second = new Subject("second");
            var correlationId = Guid.NewGuid();

            // When both instances are saved to the repository together with that correlation id
            await this.Repository.SaveAsync([first, second], correlationId);

            // Then every event carries that correlation id
            var page = await ((IEventFeed)this.EventStore).ReadEventsAsync(0, 10);
            await Assert.That(page.Events).Count().IsEqualTo(2);
            await Assert.That(page.Events.Select(static e => e.CorrelationId)).IsEquivalentTo([correlationId, correlationId]);

            // And the events follow the order of the instances, with contiguous sequence numbers
            await Assert.That(page.Events.Select(static e => ((NewSubject)e.Event).Id!)).IsEquivalentTo(["first", "second"], CollectionOrdering.Matching);
            await Assert.That(page.Events[1].SequenceNumber).IsEqualTo(page.Events[0].SequenceNumber + 1);
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

    public sealed class SaveManyLeavesOutUnchangedRoots : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an instance of an aggregate root that has been saved and loaded without changes
            await this.Repository.SaveAsync(new Subject("unchanged"));
            var unchanged = await this.Repository.LoadAsync<Subject>("unchanged");

            // And a new instance
            var created = new Subject("created");

            // When both instances are saved to the repository together
            await this.Repository.SaveAsync([unchanged, created]);

            // Then only the new instance's event was stored
            var page = await ((IEventFeed)this.EventStore).ReadEventsAsync(0, 10);
            await Assert.That(page.Events.Select(static e => ((NewSubject)e.Event).Id!)).IsEquivalentTo(["unchanged", "created"], CollectionOrdering.Matching);

            // And the unchanged instance can still be changed and saved
            var reloaded = await this.Repository.LoadAsync<Subject>("unchanged");
            await Assert.That(reloaded.GetRevision()).IsEqualTo(1);
            await this.Repository.SaveAsync([unchanged]);
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

    public sealed class SaveManyOfNoneDoesNothing : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // When no instances are saved to the repository together
            await this.Repository.SaveAsync([]);

            // Then nothing was stored
            var page = await ((IEventFeed)this.EventStore).ReadEventsAsync(0, 10);
            await Assert.That(page.Events).IsEmpty();

            // And a null collection is rejected
            await Assert.That(() => this.Repository.SaveAsync((IReadOnlyCollection<AggregateRoot>)null!)).Throws<ArgumentNullException>();
        }
    }

    public sealed class SaveManyValidatesEveryRootFirst : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a new instance of an aggregate root
            var valid = new Subject("valid");

            // And a new instance that has no events
            var empty = Subject.WithoutEvents("empty");

            // When both instances are saved to the repository together
            Func<Task> save = () => this.Repository.SaveAsync([valid, empty]);

            // Then the save fails because there are no events to save
            await Assert.That(save).Throws<PersistenceException>().WithMessageContaining("there are no events to save");

            // And not even the identity of the valid instance was registered
            await Assert.That(await this.IdentityMap.TryGetAsync(typeof(Subject), typeof(string), "valid")).IsNull();

            // And a null instance is rejected
            await Assert.That(() => this.Repository.SaveAsync([valid, null!])).Throws<ArgumentException>();
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

            public static Subject WithoutEvents(string id) => new() { Id = id };

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

    public sealed class SaveManyRejectsTheSameRootTwice : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a new instance of an aggregate root
            var subject = new Subject("twice");

            // When that instance is saved to the repository twice in one call
            Func<Task> save = () => this.Repository.SaveAsync([subject, subject]);

            // Then the save is rejected
            await Assert.That(save).Throws<ArgumentException>();

            // And nothing was saved
            await Assert.That(() => (Task)this.Repository.LoadAsync<Subject>("twice")).Throws<AggregateRootNotFoundException>();
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

    public sealed class SaveManyRejectsTwoRootsWithOneNaturalKey : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given two new instances of an aggregate root with the same natural key
            var first = new Subject("same");
            var second = new Subject("same");

            // When both instances are saved to the repository together
            Func<Task> save = () => this.Repository.SaveAsync([first, second]);

            // Then a concurrency exception names the natural key
            await Assert.That(save)
                .Throws<ConcurrencyException>()
                .WithMessageContaining($"'{typeof(Subject)}' with natural key 'same'");

            // And nothing was saved
            await Assert.That(() => (Task)this.Repository.LoadAsync<Subject>("same")).Throws<AggregateRootNotFoundException>();
            await Assert.That(first.GetUncommittedEvents()).Count().IsEqualTo(1);
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

    public sealed class SaveManyWithAnEventStoreThatCannotCommitStreams : SqlServerEventPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given an event store repository over an event store that commits one stream at a time
            var repository = new EventStoreRepository(this.IdentityMap, new SingleStreamEventStore(this.EventStore), this.SnapshotStore);

            // When two new instances are saved to that repository together
            Func<Task> save = () => repository.SaveAsync([new Subject("first"), new Subject("second")]);

            // Then the save is not supported, with advice
            await Assert.That(save).Throws<NotSupportedException>().WithMessageContaining("To fix this issue");

            // And a single instance can still be saved
            var single = new Subject("single");
            await repository.SaveAsync(single);
            await Assert.That(await repository.LoadAsync<Subject>("single")).IsEqualTo(single);
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

        private sealed class SingleStreamEventStore(IEventStore inner) : IEventStore
        {
            public Task<StreamResult> GetStreamAsync(Guid streamId, int streamRevision, CancellationToken cancellationToken = default) =>
                inner.GetStreamAsync(streamId, streamRevision, cancellationToken);

            public Task<string> CommitStreamAsync(Guid streamId, IReadOnlyList<object> events, Guid correlationId, string? preCommitState, CancellationToken cancellationToken = default) =>
                inner.CommitStreamAsync(streamId, events, correlationId, preCommitState, cancellationToken);
        }
    }
}
