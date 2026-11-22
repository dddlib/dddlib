using dddlib.Configuration;
using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;
using TUnit.Assertions.Enums;

namespace dddlib.Persistence.EventDispatcher.Tests.Features;

// As someone who uses dddlib with event sourcing
// In order to react to persisted events
// I need the SQL Server event dispatcher to deliver the events an aggregate root has applied
public abstract class SqlServerEventDispatcher : Feature
{
    [ClassDataSource<DispatcherDatabase>(Shared = SharedType.PerClass)]
    public required DispatcherDatabase Database { get; init; }

    protected string ConnectionString => this.Database.ConnectionString;

    public sealed class CanDispatch : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL Server event dispatcher
            var dispatched = new TaskCompletionSource<NewSubject>();
            await using var eventDispatcher = new SqlServer.SqlServerEventDispatcher(
                this.ConnectionString,
                (sequenceNumber, @event) => dispatched.TrySetResult((NewSubject)@event),
                new EventDispatcherOptions { DispatcherId = Guid.NewGuid(), PollingInterval = TimeSpan.FromMilliseconds(100) });
            eventDispatcher.Start();

            // And a repository over the same database
            var repository = new EventStoreRepository(
                new SqlServerIdentityMap(this.ConnectionString),
                new SqlServerEventStore(this.ConnectionString),
                new SqlServerSnapshotStore(this.ConnectionString));

            // And an instance of an aggregate root
            var instance = new Subject("key");

            // When that instance is saved to the repository
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
    public sealed class CanDispatchFromMementoRepository : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL Server event dispatcher
            var dispatched = new TaskCompletionSource<NewSubject>();
            await using var eventDispatcher = new SqlServer.SqlServerEventDispatcher(
                this.ConnectionString,
                (sequenceNumber, @event) => dispatched.TrySetResult((NewSubject)@event),
                new EventDispatcherOptions { DispatcherId = Guid.NewGuid(), PollingInterval = TimeSpan.FromMilliseconds(100) });
            eventDispatcher.Start();

            // And a memento repository over the same database
            var repository = new SqlServerMementoRepository<Subject>(this.ConnectionString);

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
    public sealed class CanDispatchPositionalRecordEvent : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL Server event dispatcher
            var dispatched = new TaskCompletionSource<object>();
            await using var eventDispatcher = new SqlServer.SqlServerEventDispatcher(
                this.ConnectionString,
                (sequenceNumber, @event) => dispatched.TrySetResult(@event),
                new EventDispatcherOptions { DispatcherId = Guid.NewGuid(), PollingInterval = TimeSpan.FromMilliseconds(100) });
            eventDispatcher.Start();

            // And a repository over the same database
            var repository = new EventStoreRepository(
                new SqlServerIdentityMap(this.ConnectionString),
                new SqlServerEventStore(this.ConnectionString),
                new SqlServerSnapshotStore(this.ConnectionString));

            // And an instance of an aggregate root that applies a positional record
            var instance = new Subject("key");

            // When that instance is saved to the repository
            await repository.SaveAsync(instance);

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

    // A repository with custom storage can append metadata the event does not carry, such as a tenant, and the
    // dispatcher receives it with dddlib's own entries (https://github.com/dddlib/dddlib/issues/52).
    public sealed class CanDispatchCallerMetadata : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL Server event dispatcher
            var dispatched = new TaskCompletionSource<IReadOnlyDictionary<string, string>>();
            await using var eventDispatcher = new SqlServer.SqlServerEventDispatcher(
                this.ConnectionString,
                new CustomEventDispatcher((sequenceNumber, metadata, @event, cancellationToken) =>
                {
                    dispatched.TrySetResult(metadata);
                    return Task.CompletedTask;
                }),
                new EventDispatcherOptions { DispatcherId = Guid.NewGuid(), PollingInterval = TimeSpan.FromMilliseconds(100) });
            eventDispatcher.Start();

            // And a repository over the same database that appends the events with a tenant
            var repository = new MetadataRepository<Subject>(this.ConnectionString, new Dictionary<string, string> { ["tenant"] = "acme" });

            // When an instance of an aggregate root is saved to the repository
            await repository.SaveAsync(new Subject("key"));

            // Then the tenant is dispatched beside the hostname and timestamp
            var metadata = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(metadata.Keys).IsEquivalentTo(["hostname", "timestamp", "tenant"]);
            await Assert.That(metadata["tenant"]).IsEqualTo("acme");
            await Assert.That(metadata["hostname"]).IsEqualTo(Environment.MachineName);
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

    public sealed class CanDispatchWithoutCallerMetadata : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL Server event dispatcher
            var dispatched = new TaskCompletionSource<IReadOnlyDictionary<string, string>>();
            await using var eventDispatcher = new SqlServer.SqlServerEventDispatcher(
                this.ConnectionString,
                new CustomEventDispatcher((sequenceNumber, metadata, @event, cancellationToken) =>
                {
                    dispatched.TrySetResult(metadata);
                    return Task.CompletedTask;
                }),
                new EventDispatcherOptions { DispatcherId = Guid.NewGuid(), PollingInterval = TimeSpan.FromMilliseconds(100) });
            eventDispatcher.Start();

            // And a memento repository over the same database
            var repository = new SqlServerMementoRepository<Subject>(this.ConnectionString);

            // When an instance of an aggregate root is saved to the repository
            await repository.SaveAsync(new Subject("key"));

            // Then only the hostname and timestamp are dispatched
            var metadata = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(metadata.Keys).IsEquivalentTo(["hostname", "timestamp"]);
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

    public sealed class CallerMetadataCannotReplaceDddlibsOwn : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository that appends the events with a hostname of its own
            var repository = new MetadataRepository<Subject>(this.ConnectionString, new Dictionary<string, string> { ["hostname"] = "web-01" });

            // When an instance of an aggregate root is saved to the repository
            var save = () => repository.SaveAsync(new Subject("key"));

            // Then the save fails
            await Assert.That(save).Throws<ArgumentException>().WithMessageContaining("The metadata key 'hostname' is reserved for dddlib.");

            // And no event is written
            await Assert.That(await new SqlServerEventStore(this.ConnectionString).GetLastSequenceNumberAsync()).IsEqualTo(0);
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

    // Each event can carry metadata of its own, such as a message ID, beside the metadata of the append
    // (https://github.com/dddlib/dddlib/issues/61).
    public sealed class CanDispatchPerEventMetadata : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL Server event dispatcher
            var dispatched = new List<IReadOnlyDictionary<string, string>>();
            var both = new TaskCompletionSource();
            await using var eventDispatcher = new SqlServer.SqlServerEventDispatcher(
                this.ConnectionString,
                new CustomEventDispatcher((sequenceNumber, metadata, @event, cancellationToken) =>
                {
                    lock (dispatched)
                    {
                        dispatched.Add(metadata);
                        if (dispatched.Count == 2)
                        {
                            both.TrySetResult();
                        }
                    }

                    return Task.CompletedTask;
                }),
                new EventDispatcherOptions { DispatcherId = Guid.NewGuid(), PollingInterval = TimeSpan.FromMilliseconds(100) });
            eventDispatcher.Start();

            // And a repository that appends the events with a tenant, and each event with a message ID of its own
            var repository = new PerEventMetadataRepository<Subject>(
                this.ConnectionString,
                new Dictionary<string, string> { ["tenant"] = "acme" },
                [new Dictionary<string, string> { ["message_id"] = "a" }, new Dictionary<string, string> { ["message_id"] = "b" }]);

            // When an instance of an aggregate root with two events is saved to the repository
            var instance = new Subject("key");
            instance.Rename("name");
            await repository.SaveAsync(instance);

            // Then each event is dispatched with the hostname, timestamp, tenant and its own message ID
            await both.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(dispatched[0].Keys).IsEquivalentTo(["hostname", "timestamp", "tenant", "message_id"]);
            await Assert.That(dispatched[0]["tenant"]).IsEqualTo("acme");
            await Assert.That(dispatched[0]["message_id"]).IsEqualTo("a");
            await Assert.That(dispatched[1].Keys).IsEquivalentTo(["hostname", "timestamp", "tenant", "message_id"]);
            await Assert.That(dispatched[1]["tenant"]).IsEqualTo("acme");
            await Assert.That(dispatched[1]["message_id"]).IsEqualTo("b");

            // And both events of the append have the same timestamp
            await Assert.That(dispatched[1]["timestamp"]).IsEqualTo(dispatched[0]["timestamp"]);
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

            public string? Name { get; private set; }

            public void Rename(string name) => this.Apply(new SubjectRenamed { Name = name });

            protected override object? GetState() => this.Id;

            protected override void SetState(object memento) => this.Id = memento.ToString();

            private void Handle(NewSubject @event) => this.Id = @event.Id;

            private void Handle(SubjectRenamed @event) => this.Name = @event.Name;
        }

        public class NewSubject
        {
            public string? Id { get; set; }
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

    public sealed class PerEventMetadataOverridesTheAppendsMetadata : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL Server event dispatcher
            var dispatched = new List<IReadOnlyDictionary<string, string>>();
            var both = new TaskCompletionSource();
            await using var eventDispatcher = new SqlServer.SqlServerEventDispatcher(
                this.ConnectionString,
                new CustomEventDispatcher((sequenceNumber, metadata, @event, cancellationToken) =>
                {
                    lock (dispatched)
                    {
                        dispatched.Add(metadata);
                        if (dispatched.Count == 2)
                        {
                            both.TrySetResult();
                        }
                    }

                    return Task.CompletedTask;
                }),
                new EventDispatcherOptions { DispatcherId = Guid.NewGuid(), PollingInterval = TimeSpan.FromMilliseconds(100) });
            eventDispatcher.Start();

            // And a repository that appends the events with a region, and the first event with a region of its own
            var repository = new PerEventMetadataRepository<CanDispatchPerEventMetadata.Subject>(
                this.ConnectionString,
                new Dictionary<string, string> { ["region"] = "eu" },
                [new Dictionary<string, string> { ["region"] = "us" }, null]);

            // When an instance of an aggregate root with two events is saved to the repository
            var instance = new CanDispatchPerEventMetadata.Subject("key");
            instance.Rename("name");
            await repository.SaveAsync(instance);

            // Then the first event's region wins for that event only
            await both.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(dispatched[0]["region"]).IsEqualTo("us");
            await Assert.That(dispatched[1]["region"]).IsEqualTo("eu");
        }

        private sealed class BootStrapper : IBootstrap<CanDispatchPerEventMetadata.Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<CanDispatchPerEventMetadata.Subject>().ToReconstituteUsing(() => new CanDispatchPerEventMetadata.Subject());
            }
        }
    }

    public sealed class PerEventMetadataMustMatchTheEvents : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository that appends two events with the metadata of one
            var repository = new PerEventMetadataRepository<CanDispatchPerEventMetadata.Subject>(
                this.ConnectionString,
                new Dictionary<string, string>(),
                [new Dictionary<string, string> { ["message_id"] = "a" }]);

            // When an instance of an aggregate root with two events is saved to the repository
            var instance = new CanDispatchPerEventMetadata.Subject("key");
            instance.Rename("name");
            var save = () => repository.SaveAsync(instance);

            // Then the save fails
            await Assert.That(save).Throws<ArgumentException>().WithMessageContaining("The event metadata has 1 entries for 2 events.");

            // And no event is written
            await Assert.That(await new SqlServerEventStore(this.ConnectionString).GetLastSequenceNumberAsync()).IsEqualTo(0);
        }

        private sealed class BootStrapper : IBootstrap<CanDispatchPerEventMetadata.Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<CanDispatchPerEventMetadata.Subject>().ToReconstituteUsing(() => new CanDispatchPerEventMetadata.Subject());
            }
        }
    }

    public sealed class PerEventMetadataCannotReplaceDddlibsOwn : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository that appends the second event with a timestamp of its own
            var repository = new PerEventMetadataRepository<CanDispatchPerEventMetadata.Subject>(
                this.ConnectionString,
                new Dictionary<string, string>(),
                [null, new Dictionary<string, string> { ["timestamp"] = "yesterday" }]);

            // When an instance of an aggregate root with two events is saved to the repository
            var instance = new CanDispatchPerEventMetadata.Subject("key");
            instance.Rename("name");
            var save = () => repository.SaveAsync(instance);

            // Then the save fails
            await Assert.That(save).Throws<ArgumentException>().WithMessageContaining("The metadata key 'timestamp' is reserved for dddlib.");

            // And no event is written
            await Assert.That(await new SqlServerEventStore(this.ConnectionString).GetLastSequenceNumberAsync()).IsEqualTo(0);
        }

        private sealed class BootStrapper : IBootstrap<CanDispatchPerEventMetadata.Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<CanDispatchPerEventMetadata.Subject>().ToReconstituteUsing(() => new CanDispatchPerEventMetadata.Subject());
            }
        }
    }

    // Stores nothing but the events, which it appends with the metadata it was given, shared and per event.
    private sealed class PerEventMetadataRepository<T>(
        string connectionString,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyList<IReadOnlyDictionary<string, string>?> eventMetadata) : SqlServerRepository<T>(connectionString)
        where T : AggregateRoot
    {
        protected override async Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, Guid correlationId, string? preCommitState, CancellationToken cancellationToken)
        {
            var state = Guid.NewGuid().ToString("N");

            await using var connection = new SqlConnection(this.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await this.AppendEventsAsync(transaction, id, metadata, events, eventMetadata, correlationId, state, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return state;
        }

        protected override Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<MementoResult?>(null);
    }

    // Stores nothing but the events, which it appends with the metadata it was given.
    private sealed class MetadataRepository<T>(string connectionString, IReadOnlyDictionary<string, string> metadata) : SqlServerRepository<T>(connectionString)
        where T : AggregateRoot
    {
        protected override async Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, Guid correlationId, string? preCommitState, CancellationToken cancellationToken)
        {
            var state = Guid.NewGuid().ToString("N");

            await using var connection = new SqlConnection(this.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await this.AppendEventsAsync(transaction, id, metadata, events, correlationId, state, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return state;
        }

        protected override Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<MementoResult?>(null);
    }

    public sealed class CanDispatchFromAnEventStoreBatch : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given an event dispatcher
            var dispatched = new List<string>();
            var both = new TaskCompletionSource();
            await using var eventDispatcher = new SqlServer.SqlServerEventDispatcher(
                this.ConnectionString,
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
                new EventDispatcherOptions { DispatcherId = Guid.NewGuid(), PollingInterval = TimeSpan.FromMilliseconds(100) });
            eventDispatcher.Start();

            // When two instances of an aggregate root are saved together to the event store repository
            await new SqlServerEventStoreRepository(this.ConnectionString).SaveAsync([new Subject("first"), new Subject("second")]);

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

    public sealed class CanDispatchFromAMementoBatch : SqlServerEventDispatcher
    {
        [Test]
        public async Task Scenario()
        {
            // Given an event dispatcher
            var dispatched = new List<object>();
            var both = new TaskCompletionSource();
            await using var eventDispatcher = new SqlServer.SqlServerEventDispatcher(
                this.ConnectionString,
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
                new EventDispatcherOptions { DispatcherId = Guid.NewGuid(), PollingInterval = TimeSpan.FromMilliseconds(100) });
            eventDispatcher.Start();

            // And memento repositories for two types of aggregate root
            var subjects = new SqlServerMementoRepository<Subject>(this.ConnectionString);
            var others = new SqlServerMementoRepository<Other>(this.ConnectionString);

            // When a new instance of each is saved in one batch
            var batch = new SqlServerBatch(this.ConnectionString);
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
