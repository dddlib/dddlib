using System.Data;
using dddlib.Configuration;
using dddlib.Persistence.Memory;
using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
using dddlib.TestFramework;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.Tests.Features;

// As someone who uses dddlib without event sourcing
// In order to persist aggregate roots durably
// I need a SQL Server memento repository to save and load aggregate roots
public abstract class SqlServerMementoPersistence : SqlServerFeature
{
    // A repository with its own table shaped for the aggregate root, built on SqlServerRepository<T>.
    public sealed class DefaultSqlServerPersistence : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL table for the subject
            await this.Database.ExecuteScriptAsync(@"CREATE TABLE [dbo].[Subjects]
(
    [Id] [uniqueidentifier] NOT NULL,
    [NaturalKey] [nvarchar](MAX) NOT NULL,
    [State] [varchar](36) NOT NULL,
    CONSTRAINT [PK_Subject] PRIMARY KEY CLUSTERED ([Id])
);");

            // And a repository
            var repository = new SubjectRepository(this.ConnectionString);

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

        public sealed class SubjectRepository(string connectionString) : SqlServerRepository<Subject>(connectionString)
        {
            protected override async Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, string? preCommitState, CancellationToken cancellationToken)
            {
                await using var connection = new SqlConnection(this.ConnectionString);
                await using var command = connection.CreateCommand();
                command.CommandText = @"MERGE [dbo].[Subjects] AS [Target]
USING (SELECT @Id AS [Id], @NaturalKey AS [NaturalKey], @State AS [State]) AS [Source]
ON [Target].[Id] = [Source].[Id]
WHEN MATCHED AND [Target].[State] = [Source].[State] THEN
    UPDATE SET [Target].[NaturalKey] = [Source].[NaturalKey], [Target].[State] = LEFT(NEWID(), 8)
WHEN NOT MATCHED AND [Source].[State] IS NULL THEN
    INSERT ([Id], [NaturalKey], [State]) VALUES ([Source].[Id], [Source].[NaturalKey], LEFT(NEWID(), 8))
OUTPUT inserted.[State];";
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
                command.Parameters.Add("@NaturalKey", SqlDbType.NVarChar, -1).Value = (string)memento;
                command.Parameters.Add("@State", SqlDbType.VarChar, 36).Value = (object?)preCommitState ?? DBNull.Value;

                await connection.OpenAsync(cancellationToken);
                var state = await command.ExecuteScalarAsync(cancellationToken);

                return state as string ?? throw new ConcurrencyException("Commit state mismatch.");
            }

            protected override async Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
            {
                await using var connection = new SqlConnection(this.ConnectionString);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT [NaturalKey], [State] FROM [dbo].[Subjects] WHERE [Id] = @Id;";
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;

                await connection.OpenAsync(cancellationToken);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                return await reader.ReadAsync(cancellationToken)
                    ? new MementoResult(reader.GetString(0), reader.GetString(1))
                    : null;
            }
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

    // The shipped SqlServerMementoRepository<T>, which stores any memento as JSON in the Mementos table.
    public sealed class DefaultMementoRepositoryPersistence : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository
            var repository = new SqlServerMementoRepository<Subject>(this.ConnectionString);

            // And an instance of an aggregate root with a natural key
            var instance = new Subject("key") { Name = "first" };

            // And that instance is saved and loaded
            await repository.SaveAsync(instance);
            var loaded = await repository.LoadAsync(instance.NaturalKey!);

            // When the loaded instance is changed, saved, and loaded again
            loaded.Name = "second";
            await repository.SaveAsync(loaded);
            var reloaded = await repository.LoadAsync(instance.NaturalKey!);

            // Then the reloaded instance carries the change
            await Assert.That(reloaded).IsEqualTo(instance);
            await Assert.That(reloaded.Name).IsEqualTo("second");

            // And saving the stale first instance is a concurrency error
            instance.Name = "stale";
            var action = () => repository.SaveAsync(instance);
            await Assert.That(action).Throws<ConcurrencyException>();
        }

        public class Subject : AggregateRoot
        {
            public Subject(string naturalKey)
            {
                this.NaturalKey = naturalKey;
            }

            internal Subject()
            {
            }

            [NaturalKey]
            public string? NaturalKey { get; private set; }

            public string? Name { get; set; }

            protected override object? GetState() => new Memento { NaturalKey = this.NaturalKey, Name = this.Name };

            protected override void SetState(object memento)
            {
                var subject = (Memento)memento;
                this.NaturalKey = subject.NaturalKey;
                this.Name = subject.Name;
            }

            public sealed class Memento
            {
                public string? NaturalKey { get; set; }

                public string? Name { get; set; }
            }
        }
    }

    // Events applied to the aggregate root are appended to its stream in the same transaction as the memento, so
    // that they can be dispatched. The memento remains the source of state; the events are not used for reconstitution.
    public sealed class EventsAreStoredForDispatch : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository
            var repository = new SqlServerMementoRepository<Subject>(this.ConnectionString);

            // And an instance of an aggregate root with a natural key
            var instance = new Subject("key");

            // When that instance is saved to the repository
            await repository.SaveAsync(instance);

            // Then its event is in the event store under the aggregate root's identity and the instance has no uncommitted events
            var id = await new SqlServerIdentityMap(this.ConnectionString).TryGetAsync(typeof(Subject), typeof(string), "key");
            var eventStore = new SqlServerEventStore(this.ConnectionString);
            var stream = await eventStore.GetStreamAsync(id!.Value, 0);
            await Assert.That(stream.Events).Count().IsEqualTo(1);
            await Assert.That(((NewSubject)stream.Events[0]).NaturalKey).IsEqualTo("key");
            await Assert.That(instance.GetUncommittedEvents()).IsEmpty();

            // When the instance is loaded, changed and saved again
            var loaded = await repository.LoadAsync("key");
            loaded.Rename("name");
            await repository.SaveAsync(loaded);

            // Then the stream has both events and carries the memento's state token
            stream = await eventStore.GetStreamAsync(id.Value, 0);
            await Assert.That(stream.Events).Count().IsEqualTo(2);
            await Assert.That(((SubjectRenamed)stream.Events[1]).Name).IsEqualTo("name");
            await Assert.That(stream.State).IsEqualTo(loaded.State);

            // When the instance is saved again without changes
            await repository.SaveAsync(loaded);

            // Then nothing more is appended
            stream = await eventStore.GetStreamAsync(id.Value, 0);
            await Assert.That(stream.Events).Count().IsEqualTo(2);
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

    // A repository with custom storage appends the events itself, inside its own transaction, through AppendEventsAsync.
    public sealed class CustomStorageStoresEvents : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL table for the subject
            await this.Database.ExecuteScriptAsync(@"CREATE TABLE [dbo].[Subjects]
(
    [Id] [uniqueidentifier] NOT NULL,
    [NaturalKey] [nvarchar](MAX) NOT NULL,
    [State] [varchar](36) NOT NULL,
    CONSTRAINT [PK_Subject] PRIMARY KEY CLUSTERED ([Id])
);");

            // And a repository that appends the events in the same transaction as the memento
            var repository = new SubjectRepository(this.ConnectionString);

            // And an instance of an aggregate root with a natural key
            var instance = new Subject("key");

            // When that instance is saved to the repository
            await repository.SaveAsync(instance);

            // Then its event is in the event store with the memento's state token
            var id = await new SqlServerIdentityMap(this.ConnectionString).TryGetAsync(typeof(Subject), typeof(string), "key");
            var stream = await new SqlServerEventStore(this.ConnectionString).GetStreamAsync(id!.Value, 0);
            await Assert.That(stream.Events).Count().IsEqualTo(1);
            await Assert.That(((NewSubject)stream.Events[0]).NaturalKey).IsEqualTo("key");
            await Assert.That(stream.State).IsEqualTo(instance.State);

            // And an other instance loaded from the repository is that instance
            var otherInstance = await repository.LoadAsync("key");
            await Assert.That(otherInstance).IsEqualTo(instance);
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

        public sealed class SubjectRepository(string connectionString) : SqlServerRepository<Subject>(connectionString)
        {
            protected override async Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, string? preCommitState, CancellationToken cancellationToken)
            {
                await using var connection = new SqlConnection(this.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"MERGE [dbo].[Subjects] AS [Target]
USING (SELECT @Id AS [Id], @NaturalKey AS [NaturalKey], @State AS [State]) AS [Source]
ON [Target].[Id] = [Source].[Id]
WHEN MATCHED AND [Target].[State] = [Source].[State] THEN
    UPDATE SET [Target].[NaturalKey] = [Source].[NaturalKey], [Target].[State] = LEFT(NEWID(), 8)
WHEN NOT MATCHED AND [Source].[State] IS NULL THEN
    INSERT ([Id], [NaturalKey], [State]) VALUES ([Source].[Id], [Source].[NaturalKey], LEFT(NEWID(), 8))
OUTPUT inserted.[State];";
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
                command.Parameters.Add("@NaturalKey", SqlDbType.NVarChar, -1).Value = (string)memento;
                command.Parameters.Add("@State", SqlDbType.VarChar, 36).Value = (object?)preCommitState ?? DBNull.Value;

                var state = await command.ExecuteScalarAsync(cancellationToken) as string ?? throw new ConcurrencyException("Commit state mismatch.");

                await this.AppendEventsAsync(transaction, id, events, state, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return state;
            }

            protected override async Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
            {
                await using var connection = new SqlConnection(this.ConnectionString);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT [NaturalKey], [State] FROM [dbo].[Subjects] WHERE [Id] = @Id;";
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;

                await connection.OpenAsync(cancellationToken);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                return await reader.ReadAsync(cancellationToken)
                    ? new MementoResult(reader.GetString(0), reader.GetString(1))
                    : null;
            }
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

    // A repository whose identities come from its own table instead of dddlib's natural keys, so that rows that
    // already exist can be loaded without anything having written to the dddlib schema (issue 45).
    public sealed class CustomIdentityMap : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL table for the subject that holds the identity of its stream
            await this.Database.ExecuteScriptAsync(@"CREATE TABLE [dbo].[Subjects]
(
    [NaturalKey] [nvarchar](450) NOT NULL,
    [Name] [nvarchar](MAX) NULL,
    [StreamId] [uniqueidentifier] NOT NULL,
    [State] [varchar](36) NOT NULL,
    CONSTRAINT [PK_Subject] PRIMARY KEY CLUSTERED ([NaturalKey])
);");

            // And a row in that table that dddlib did not save
            var streamId = Guid.NewGuid();
            await this.Database.ExecuteScriptAsync(
                $"INSERT INTO [dbo].[Subjects] ([NaturalKey], [Name], [StreamId], [State]) VALUES ('key', 'first', '{streamId}', 'seeded');");

            // And a repository with an identity map that reads that table
            var repository = new SubjectRepository(this.ConnectionString, new SubjectIdentityMap(this.ConnectionString));

            // When an instance is loaded from the repository
            var instance = await repository.LoadAsync("key");

            // Then it is the aggregate root in that row
            await Assert.That(instance.NaturalKey).IsEqualTo("key");
            await Assert.That(instance.Name).IsEqualTo("first");

            // When that instance is changed and saved
            instance.Rename("second");
            await repository.SaveAsync(instance);

            // Then its event is in the stream the row identifies, with the memento's state token
            var stream = await new SqlServerEventStore(this.ConnectionString).GetStreamAsync(streamId, 0);
            await Assert.That(stream.Events).Count().IsEqualTo(1);
            await Assert.That(((SubjectRenamed)stream.Events[0]).Name).IsEqualTo("second");
            await Assert.That(stream.State).IsEqualTo(instance.State);

            // And an other instance loaded from the repository carries the change
            var otherInstance = await repository.LoadAsync("key");
            await Assert.That(otherInstance).IsEqualTo(instance);
            await Assert.That(otherInstance.Name).IsEqualTo("second");

            // When a new instance is saved to the repository
            var newInstance = new Subject("new");
            await repository.SaveAsync(newInstance);

            // Then it can be loaded too
            await Assert.That(await repository.LoadAsync("new")).IsEqualTo(newInstance);

            // And the dddlib identity map has neither
            var identityMap = new SqlServerIdentityMap(this.ConnectionString);
            await Assert.That(await identityMap.TryGetAsync(typeof(Subject), typeof(string), "key")).IsNull();
            await Assert.That(await identityMap.TryGetAsync(typeof(Subject), typeof(string), "new")).IsNull();
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

        // Reads the identity of the stream from the subject's own row. The identity of a new subject is stored
        // when the repository inserts its row, so there is nothing to add or remove here.
        public sealed class SubjectIdentityMap(string connectionString) : IIdentityMap
        {
            public async Task<Guid> GetOrAddAsync(Type aggregateRootType, Type naturalKeyType, object naturalKey, CancellationToken cancellationToken = default) =>
                await this.TryGetAsync(aggregateRootType, naturalKeyType, naturalKey, cancellationToken) ?? Guid.NewGuid();

            public async Task<Guid?> TryGetAsync(Type aggregateRootType, Type naturalKeyType, object naturalKey, CancellationToken cancellationToken = default)
            {
                await using var connection = new SqlConnection(connectionString);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT [StreamId] FROM [dbo].[Subjects] WHERE [NaturalKey] = @NaturalKey;";
                command.Parameters.Add("@NaturalKey", SqlDbType.NVarChar, 450).Value = (string)naturalKey;

                await connection.OpenAsync(cancellationToken);

                return await command.ExecuteScalarAsync(cancellationToken) as Guid?;
            }

            public Task RemoveAsync(Guid identity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        public sealed class SubjectRepository(string connectionString, IIdentityMap identityMap) : SqlServerRepository<Subject>(connectionString, identityMap)
        {
            protected override async Task<string> SaveAsync(Guid id, object memento, IReadOnlyList<object> events, string? preCommitState, CancellationToken cancellationToken)
            {
                var subject = (Subject.Memento)memento;

                await using var connection = new SqlConnection(this.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"MERGE [dbo].[Subjects] AS [Target]
USING (SELECT @StreamId AS [StreamId], @NaturalKey AS [NaturalKey], @Name AS [Name], @State AS [State]) AS [Source]
ON [Target].[StreamId] = [Source].[StreamId]
WHEN MATCHED AND [Target].[State] = [Source].[State] THEN
    UPDATE SET [Target].[Name] = [Source].[Name], [Target].[State] = LEFT(NEWID(), 8)
WHEN NOT MATCHED AND [Source].[State] IS NULL THEN
    INSERT ([NaturalKey], [Name], [StreamId], [State]) VALUES ([Source].[NaturalKey], [Source].[Name], [Source].[StreamId], LEFT(NEWID(), 8))
OUTPUT inserted.[State];";
                command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = id;
                command.Parameters.Add("@NaturalKey", SqlDbType.NVarChar, 450).Value = subject.NaturalKey;
                command.Parameters.Add("@Name", SqlDbType.NVarChar, -1).Value = (object?)subject.Name ?? DBNull.Value;
                command.Parameters.Add("@State", SqlDbType.VarChar, 36).Value = (object?)preCommitState ?? DBNull.Value;

                var state = await command.ExecuteScalarAsync(cancellationToken) as string ?? throw new ConcurrencyException("Commit state mismatch.");

                await this.AppendEventsAsync(transaction, id, events, state, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return state;
            }

            protected override async Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
            {
                await using var connection = new SqlConnection(this.ConnectionString);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT [NaturalKey], [Name], [State] FROM [dbo].[Subjects] WHERE [StreamId] = @StreamId;";
                command.Parameters.Add("@StreamId", SqlDbType.UniqueIdentifier).Value = id;

                await connection.OpenAsync(cancellationToken);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                return await reader.ReadAsync(cancellationToken)
                    ? new MementoResult(
                        new Subject.Memento { NaturalKey = reader.GetString(0), Name = await reader.IsDBNullAsync(1, cancellationToken) ? null : reader.GetString(1) },
                        reader.GetString(2))
                    : null;
            }
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

    // A batch saves aggregate roots of different types, each through its own repository, in one transaction.
    public sealed class BatchSavesRootsOfDifferentTypes : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given repositories for two types of aggregate root
            var subjects = new SqlServerMementoRepository<Subject>(this.ConnectionString);
            var others = new SqlServerMementoRepository<Other>(this.ConnectionString);

            // And a new instance of each
            var subject = new Subject("subject");
            var other = new Other(7);

            // When both instances are added to a batch and the batch is saved
            var batch = new SqlServerBatch(this.ConnectionString);
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

    public sealed class BatchIsAllOrNothing : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given repositories for two types of aggregate root
            var subjects = new SqlServerMementoRepository<Subject>(this.ConnectionString);
            var others = new SqlServerMementoRepository<Other>(this.ConnectionString);

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
            var batch = new SqlServerBatch(this.ConnectionString);
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
            await Assert.That((await new SqlServerEventStore(this.ConnectionString).ReadEventsAsync(0, 10)).Events).Count().IsEqualTo(2);

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

    public sealed class BatchStoresTheEventsOfEveryRoot : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given repositories for two types of aggregate root
            var subjects = new SqlServerMementoRepository<Subject>(this.ConnectionString);
            var others = new SqlServerMementoRepository<Other>(this.ConnectionString);

            // And a new instance of each
            var subject = new Subject("subject");
            var other = new Other(7);

            // When both instances are added to a batch and the batch is saved
            var batch = new SqlServerBatch(this.ConnectionString);
            subjects.Add(batch, subject);
            others.Add(batch, other);
            await batch.SaveAsync();

            // Then their events are stored in the order the instances were added, with contiguous sequence numbers
            var eventStore = new SqlServerEventStore(this.ConnectionString);
            var events = (await eventStore.ReadEventsAsync(0, 10)).Events;
            await Assert.That(events).Count().IsEqualTo(2);
            await Assert.That(events[0].Event).IsTypeOf<NewSubject>();
            await Assert.That(events[1].Event).IsTypeOf<NewOther>();
            await Assert.That(events[1].SequenceNumber).IsEqualTo(events[0].SequenceNumber + 1);

            // And they share one correlation id
            await Assert.That(events[1].CorrelationId).IsEqualTo(events[0].CorrelationId);

            // And each stream carries its memento's state token
            var subjectId = await new SqlServerIdentityMap(this.ConnectionString).TryGetAsync(typeof(Subject), typeof(string), "subject");
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

    public sealed class BatchIsSingleUse : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository
            var repository = new SqlServerMementoRepository<Subject>(this.ConnectionString);

            // And a batch that has been saved
            var batch = new SqlServerBatch(this.ConnectionString);
            repository.Add(batch, new Subject("first"));
            await batch.SaveAsync();

            // When an instance is added to that batch
            // Then the batch refuses it
            await Assert.That(() => repository.Add(batch, new Subject("second"))).Throws<InvalidOperationException>();

            // And the batch cannot be saved again
            await Assert.That(() => batch.SaveAsync()).Throws<InvalidOperationException>();

            // And a batch that failed cannot be saved again either
            var failed = new SqlServerBatch(this.ConnectionString);
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

    public sealed class EmptyBatchDoesNothing : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository and an empty batch
            var repository = new SqlServerMementoRepository<Subject>(this.ConnectionString);
            var batch = new SqlServerBatch(this.ConnectionString);

            // When the batch is saved
            await batch.SaveAsync();

            // Then nothing was saved
            await Assert.That(() => (Task)repository.LoadAsync("anything")).Throws<AggregateRootNotFoundException>();
            await Assert.That((await new SqlServerEventStore(this.ConnectionString).ReadEventsAsync(0, 10)).Events).IsEmpty();

            // And adding nothing, or to nothing, is rejected
            await Assert.That(() => repository.Add(new SqlServerBatch(this.ConnectionString), null!)).Throws<ArgumentNullException>();
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

    public sealed class BatchRejectsTheSameRootTwice : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository and a batch holding an instance of an aggregate root
            var repository = new SqlServerMementoRepository<Subject>(this.ConnectionString);
            var subject = new Subject("twice");
            var batch = new SqlServerBatch(this.ConnectionString);
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

    public sealed class BatchRejectsTwoRootsWithOneNaturalKey : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository and two new instances of an aggregate root with the same natural key
            var repository = new SqlServerMementoRepository<Subject>(this.ConnectionString);
            var first = new Subject("same");
            var second = new Subject("same");

            // When both instances are added to a batch and the batch is saved
            var batch = new SqlServerBatch(this.ConnectionString);
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

    public sealed class BatchRejectsARepositoryOfAnotherKind : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL Server repository and a memory batch
            var repository = new SqlServerMementoRepository<Subject>(this.ConnectionString);

            // When an instance is added to the memory batch through the SQL Server repository
            // Then the repository refuses it
            await Assert.That(() => repository.Add(new MemoryBatch(), new Subject("key"))).Throws<ArgumentException>();

            // And a memory repository refuses a SQL Server batch
            await Assert.That(() => new MemoryRepository<Subject>().Add(new SqlServerBatch(this.ConnectionString), new Subject("key"))).Throws<ArgumentException>();
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

    public sealed class BatchRejectsAnotherDatabase : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository over the database
            var repository = new SqlServerMementoRepository<Subject>(this.ConnectionString);

            // When an instance is added through it to a batch over another database
            var otherDatabase = new SqlConnectionStringBuilder(this.ConnectionString) { InitialCatalog = "other" }.ConnectionString;

            // Then the repository refuses it
            await Assert.That(() => repository.Add(new SqlServerBatch(otherDatabase), new Subject("key"))).Throws<ArgumentException>();

            // And it refuses a batch over another schema of the same database
            await Assert.That(() => repository.Add(new SqlServerBatch(this.ConnectionString, "other"), new Subject("key"))).Throws<ArgumentException>();

            // And it accepts a batch over the same database whatever the other connection settings
            var samePlace = new SqlConnectionStringBuilder(this.ConnectionString) { ApplicationName = "batch" }.ConnectionString;
            var batch = new SqlServerBatch(samePlace);
            repository.Add(batch, new Subject("key"));
            await batch.SaveAsync();
            await Assert.That(await repository.LoadAsync("key")).IsNotNull();
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

    // Custom storage written through the transactional hook: dddlib owns the connection and the transaction, takes the
    // identity's lock, appends the events and commits; the repository writes only its own table.
    public sealed class CustomStorageThroughTheTransactionalHook : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a SQL table for the subject and a repository that writes it through the transactional hook
            await this.Database.ExecuteScriptAsync(SubjectRepository.Table);
            var repository = new SubjectRepository(this.ConnectionString);

            // When an instance is saved, loaded, changed and saved again
            var instance = new Subject("key");
            await repository.SaveAsync(instance);
            var loaded = await repository.LoadAsync("key");
            loaded.Rename("name");
            await repository.SaveAsync(loaded);

            // Then the stored instance has the change
            await Assert.That((await repository.LoadAsync("key")).Name).IsEqualTo("name");

            // And both events are in the stream, which carries the memento's state token
            var id = await new SqlServerIdentityMap(this.ConnectionString).TryGetAsync(typeof(Subject), typeof(string), "key");
            var stream = await new SqlServerEventStore(this.ConnectionString).GetStreamAsync(id!.Value, 0);
            await Assert.That(stream.Events).Count().IsEqualTo(2);
            await Assert.That(stream.State).IsEqualTo(loaded.State);

            // And saving the first, now stale, instance again is a concurrency exception that writes nothing
            instance.Rename("stale");
            await Assert.That(() => repository.SaveAsync(instance)).Throws<ConcurrencyException>();
            await Assert.That((await repository.LoadAsync("key")).Name).IsEqualTo("name");
            await Assert.That((await new SqlServerEventStore(this.ConnectionString).GetStreamAsync(id.Value, 0)).Events).Count().IsEqualTo(2);

            // And a hook that appends events itself is refused, because dddlib appends them
            var appending = new AppendingRepository(this.ConnectionString);
            await Assert.That(() => appending.SaveAsync(new Subject("appending"))).Throws<InvalidOperationException>();
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

        public class SubjectRepository(string connectionString) : SqlServerRepository<Subject>(connectionString)
        {
            public const string Table = @"CREATE TABLE [dbo].[Subjects]
(
    [Id] [uniqueidentifier] NOT NULL,
    [NaturalKey] [nvarchar](100) NOT NULL,
    [Name] [nvarchar](100) NULL,
    [State] [varchar](36) NOT NULL,
    CONSTRAINT [PK_Subject] PRIMARY KEY CLUSTERED ([Id])
);";

            protected override async Task<string> SaveAsync(SqlTransaction transaction, Guid id, object memento, string? preCommitState, CancellationToken cancellationToken)
            {
                var subject = (Subject.Memento)memento;

                await using var command = transaction.Connection!.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"DECLARE @PostCommitState VARCHAR(36) = LEFT(NEWID(), 8);
IF @PreCommitState IS NULL
    INSERT INTO [dbo].[Subjects] ([Id], [NaturalKey], [Name], [State]) VALUES (@Id, @NaturalKey, @Name, @PostCommitState);
ELSE
BEGIN
    UPDATE [dbo].[Subjects] SET [Name] = @Name, [State] = @PostCommitState WHERE [Id] = @Id AND [State] = @PreCommitState;
    IF @@ROWCOUNT = 0
        THROW 50409, 'The subject has changed since it was loaded.', 1;
END;
SELECT @PostCommitState;";
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
                command.Parameters.Add("@NaturalKey", SqlDbType.NVarChar, 100).Value = subject.NaturalKey;
                command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = (object?)subject.Name ?? DBNull.Value;
                command.Parameters.Add("@PreCommitState", SqlDbType.VarChar, 36).Value = (object?)preCommitState ?? DBNull.Value;

                return (string)(await command.ExecuteScalarAsync(cancellationToken))!;
            }

            protected override async Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
            {
                await using var connection = new SqlConnection(this.ConnectionString);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT [NaturalKey], [Name], [State] FROM [dbo].[Subjects] WHERE [Id] = @Id;";
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;

                await connection.OpenAsync(cancellationToken);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                return await reader.ReadAsync(cancellationToken)
                    ? new MementoResult(new Subject.Memento { NaturalKey = reader.GetString(0), Name = reader.IsDBNull(1) ? null : reader.GetString(1) }, reader.GetString(2))
                    : null;
            }
        }

        public sealed class AppendingRepository(string connectionString) : SubjectRepository(connectionString)
        {
            protected override async Task<string> SaveAsync(SqlTransaction transaction, Guid id, object memento, string? preCommitState, CancellationToken cancellationToken)
            {
                await this.AppendEventsAsync(transaction, id, [new NewSubject { NaturalKey = "appended" }], "state", cancellationToken);
                return "state";
            }
        }

        private sealed class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    // The batch takes custom storage and the built-in table together; a conflict in either writes nothing in both.
    public sealed class BatchMixesBuiltInAndCustomStorage : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository with custom storage and a repository over the built-in table
            await this.Database.ExecuteScriptAsync(CustomStorageThroughTheTransactionalHook.SubjectRepository.Table);
            var subjects = new CustomStorageThroughTheTransactionalHook.SubjectRepository(this.ConnectionString);
            var others = new SqlServerMementoRepository<Other>(this.ConnectionString);

            // When a new instance of each is saved in one batch
            var subject = new CustomStorageThroughTheTransactionalHook.Subject("subject");
            var other = new Other(7);
            var batch = new SqlServerBatch(this.ConnectionString);
            subjects.Add(batch, subject);
            others.Add(batch, other);
            await batch.SaveAsync();

            // Then both load as they were saved
            await Assert.That(await subjects.LoadAsync("subject")).IsEqualTo(subject);
            await Assert.That(await others.LoadAsync(7)).IsEqualTo(other);

            // When the custom-storage instance is stale and a batch saves it with a new instance of the other type
            var winner = await subjects.LoadAsync("subject");
            winner.Rename("winner");
            await subjects.SaveAsync(winner);
            subject.Rename("loser");
            var another = new Other(8);
            var failing = new SqlServerBatch(this.ConnectionString);
            others.Add(failing, another);
            subjects.Add(failing, subject);
            Func<Task> save = () => failing.SaveAsync();

            // Then a concurrency exception names the stale instance
            await Assert.That(save)
                .Throws<ConcurrencyException>()
                .WithMessageContaining($"'{typeof(CustomStorageThroughTheTransactionalHook.Subject)}' with natural key 'subject'");

            // And the built-in table was not written either
            await Assert.That(() => (Task)others.LoadAsync(8)).Throws<AggregateRootNotFoundException>();
            await Assert.That((await subjects.LoadAsync("subject")).Name).IsEqualTo("winner");
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

        private sealed class SubjectBootStrapper : IBootstrap<CustomStorageThroughTheTransactionalHook.Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<CustomStorageThroughTheTransactionalHook.Subject>()
                    .ToReconstituteUsing(() => new CustomStorageThroughTheTransactionalHook.Subject());
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

    // Metadata the events do not carry, such as a tenant, comes from the repository, alone or in a batch.
    public sealed class CustomStorageEventMetadata : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository with custom storage that gives its events a tenant
            await this.Database.ExecuteScriptAsync(CustomStorageThroughTheTransactionalHook.SubjectRepository.Table);
            var repository = new TenantRepository(this.ConnectionString, "acme");

            // When one instance is saved alone and another in a batch
            await repository.SaveAsync(new CustomStorageThroughTheTransactionalHook.Subject("alone"));
            var batch = new SqlServerBatch(this.ConnectionString);
            repository.Add(batch, new CustomStorageThroughTheTransactionalHook.Subject("batched"));
            await batch.SaveAsync();

            // Then both events carry the tenant beside dddlib's own metadata
            for (var sequenceNumber = 1; sequenceNumber <= 2; sequenceNumber++)
            {
                var metadata = (string?)await this.Database.ExecuteScalarAsync($"SELECT [Metadata] FROM [dbo].[Events] WHERE [SequenceNumber] = {sequenceNumber};");
                await Assert.That(metadata).Contains("\"tenant\":\"acme\"");
                await Assert.That(metadata).Contains("\"hostname\":");
            }
        }

        public sealed class TenantRepository(string connectionString, string tenant) : CustomStorageThroughTheTransactionalHook.SubjectRepository(connectionString)
        {
            protected override IReadOnlyDictionary<string, string>? EventMetadata => new Dictionary<string, string> { ["tenant"] = tenant };
        }

        private sealed class BootStrapper : IBootstrap<CustomStorageThroughTheTransactionalHook.Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<CustomStorageThroughTheTransactionalHook.Subject>()
                    .ToReconstituteUsing(() => new CustomStorageThroughTheTransactionalHook.Subject());
            }
        }
    }

    // A repository that overrides the original hook owns its transaction, so it cannot share the batch's.
    public sealed class CustomStorageWithTheOldHookCannotJoinABatch : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository that overrides the original hook
            await this.Database.ExecuteScriptAsync(@"CREATE TABLE [dbo].[Subjects]
(
    [Id] [uniqueidentifier] NOT NULL,
    [NaturalKey] [nvarchar](MAX) NOT NULL,
    [State] [varchar](36) NOT NULL,
    CONSTRAINT [PK_Subject] PRIMARY KEY CLUSTERED ([Id])
);");
            var repository = new CustomStorageStoresEvents.SubjectRepository(this.ConnectionString);

            // When an instance is added to a batch through it
            // Then the repository refuses it, with advice
            await Assert.That(() => repository.Add(new SqlServerBatch(this.ConnectionString), new CustomStorageStoresEvents.Subject("key")))
                .Throws<NotSupportedException>()
                .WithMessageContaining("To fix this issue");

            // And it still saves on its own
            var instance = new CustomStorageStoresEvents.Subject("key");
            await repository.SaveAsync(instance);
            await Assert.That(await repository.LoadAsync("key")).IsEqualTo(instance);

            // And a repository that overrides neither hook fails its first save, with advice
            await Assert.That(() => new HooklessRepository(this.ConnectionString).SaveAsync(new CustomStorageStoresEvents.Subject("other")))
                .Throws<NotSupportedException>()
                .WithMessageContaining("To fix this issue");
        }

        public sealed class HooklessRepository(string connectionString) : SqlServerRepository<CustomStorageStoresEvents.Subject>(connectionString)
        {
            protected override Task<MementoResult?> LoadAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<MementoResult?>(null);
        }

        private sealed class BootStrapper : IBootstrap<CustomStorageStoresEvents.Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<CustomStorageStoresEvents.Subject>()
                    .ToUseNaturalKey(subject => subject.NaturalKey)
                    .ToReconstituteUsing(() => new CustomStorageStoresEvents.Subject());
            }
        }
    }

    // Custom storage that checks its state token with row locks, as tables shaped for the aggregate root typically do.
    // Batches in opposite orders and single saves of the same instances run together: any of them may lose a race (a
    // ConcurrencyException), but none may be a deadlock victim, because every save takes the identity locks first.
    public sealed class BatchAndSingleSavesDoNotDeadlock : SqlServerMementoPersistence
    {
        [Test]
        public async Task Scenario()
        {
            // Given a repository with custom storage and three saved instances
            await this.Database.ExecuteScriptAsync(CustomStorageThroughTheTransactionalHook.SubjectRepository.Table);
            var repository = new CustomStorageThroughTheTransactionalHook.SubjectRepository(this.ConnectionString);
            string[] keys = ["a", "b", "c"];
            foreach (var key in keys)
            {
                await repository.SaveAsync(new CustomStorageThroughTheTransactionalHook.Subject(key));
            }

            var batches = 0;

            async Task SaveManyAsync(IEnumerable<string> order)
            {
                for (var round = 0; round < 15; round++)
                {
                    var batch = new SqlServerBatch(this.ConnectionString);
                    foreach (var key in order)
                    {
                        var instance = await repository.LoadAsync(key);
                        instance.Rename($"batch {round}");
                        repository.Add(batch, instance);
                    }

                    try
                    {
                        await batch.SaveAsync();
                        Interlocked.Increment(ref batches);
                    }
                    catch (ConcurrencyException)
                    {
                    }
                }
            }

            async Task SaveOneAsync(string key)
            {
                for (var round = 0; round < 15; round++)
                {
                    var instance = await repository.LoadAsync(key);
                    instance.Rename($"single {round}");
                    try
                    {
                        await repository.SaveAsync(instance);
                    }
                    catch (ConcurrencyException)
                    {
                    }
                }
            }

            // When batches in both orders and single saves run together
            await Task.WhenAll(SaveManyAsync(keys), SaveManyAsync(keys.Reverse()), SaveOneAsync("a"), SaveOneAsync("b"), SaveOneAsync("c"));

            // Then no save was a deadlock victim, and some batches succeeded
            await Assert.That(batches).IsGreaterThan(0);
        }

        private sealed class BootStrapper : IBootstrap<CustomStorageThroughTheTransactionalHook.Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<CustomStorageThroughTheTransactionalHook.Subject>()
                    .ToReconstituteUsing(() => new CustomStorageThroughTheTransactionalHook.Subject());
            }
        }
    }
}
