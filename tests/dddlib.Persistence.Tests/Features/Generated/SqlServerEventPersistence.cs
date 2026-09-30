using dddlib.Configuration;
using dddlib.Persistence.SqlServer;
using dddlib.Persistence.Sdk;
using dddlib.TestFramework;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Tests.Features.Generated;

// As someone who uses dddlib with event sourcing
// In order to persist aggregate roots durably
// I need the SQL Server event store repository to save and load aggregate roots
public abstract partial class SqlServerEventPersistence : SqlServerFeature
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

    public sealed partial class UndefinedNaturalKey : SqlServerEventPersistence
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

        public partial class Subject : AggregateRoot
        {
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed partial class UndefinedUnititializedFactory : SqlServerEventPersistence
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

        public partial class Subject : AggregateRoot
        {
            public Subject(string nonsense)
            {
                _ = nonsense;
            }

            [NaturalKey]
            public string? Id { get; set; }
        }
    }

    public sealed partial class NullNaturalKey : SqlServerEventPersistence
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

        public partial class Subject : AggregateRoot
        {
            [NaturalKey]
            public string? Id { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed partial class SaveAndLoad : SqlServerEventPersistence
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

        public partial class Subject : AggregateRoot
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

        public partial class NewSubject
        {
            public string? Id { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed partial class SaveAndSaveAndLoad : SqlServerEventPersistence
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

        public partial class Subject : AggregateRoot
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

            private sealed partial class Memento
            {
                public string? Id { get; set; }

                public bool HasDoneSomething { get; set; }
            }
        }

        public partial class NewSubject
        {
            public string? Id { get; set; }
        }

        public partial class SubjectDidSomething
        {
            public string? Id { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed partial class SaveAndLoadAndSaveAndLoad : SqlServerEventPersistence
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

        public partial class Subject : AggregateRoot
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

            private sealed partial class Memento
            {
                public string? Id { get; set; }

                public bool HasDoneSomething { get; set; }
            }
        }

        public partial class NewSubject
        {
            public string? Id { get; set; }
        }

        public partial class SubjectDidSomething
        {
            public string? Id { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed partial class SnapshotAndLoad : SqlServerEventPersistence
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

        public partial class Subject : AggregateRoot
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

        public partial class NewSubject
        {
            public string? Id { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed partial class SnapshotAndSaveAndLoad : SqlServerEventPersistence
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

        public partial class Subject : AggregateRoot
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

            private sealed partial class Memento
            {
                public string? Id { get; set; }

                public bool HasDoneSomething { get; set; }
            }
        }

        public partial class NewSubject
        {
            public string? Id { get; set; }
        }

        public partial class SubjectDidSomething
        {
            public string? Id { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    public sealed partial class SaveAndEndLifecycleAndSaveAndCreate : SqlServerEventPersistence
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

        public partial class Subject : AggregateRoot
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

        public partial class NewSubject
        {
            public string? Id { get; set; }
        }

        public partial class SubjectDestroyed
        {
            public string? Id { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    // A positional record has no parameterless constructor; it is read back through its primary constructor
    // (https://github.com/dddlib/dddlib/issues/48).
    public sealed partial class SaveAndLoadWithPositionalRecordEvents : SqlServerEventPersistence
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

        public partial class Subject : AggregateRoot
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

        public partial record NewSubject(string Id);

        public partial record SubjectRenamed(string Id, string Name);

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }
}
