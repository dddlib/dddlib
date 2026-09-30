using System.Data;
using dddlib.Configuration;
using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.Tests.Features.Generated;

// As someone who uses dddlib without event sourcing
// In order to persist aggregate roots durably
// I need a SQL Server memento repository to save and load aggregate roots
public abstract partial class SqlServerMementoPersistence : SqlServerFeature
{
    // A repository with its own table shaped for the aggregate root, built on SqlServerRepository<T>.
    public sealed partial class DefaultSqlServerPersistence : SqlServerMementoPersistence
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

        public partial class Subject : AggregateRoot
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

        public partial class NewSubject
        {
            public string? NaturalKey { get; set; }
        }

        public sealed partial class SubjectRepository(string connectionString) : SqlServerRepository<Subject>(connectionString)
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

        private sealed partial class BootStrapper : IBootstrap<Subject>
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
    public sealed partial class DefaultMementoRepositoryPersistence : SqlServerMementoPersistence
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

        public partial class Subject : AggregateRoot
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

            public sealed partial class Memento
            {
                public string? NaturalKey { get; set; }

                public string? Name { get; set; }
            }
        }
    }

    // Events applied to the aggregate root are appended to its stream in the same transaction as the memento, so
    // that they can be dispatched. The memento remains the source of state; the events are not used for reconstitution.
    public sealed partial class EventsAreStoredForDispatch : SqlServerMementoPersistence
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

        public partial class Subject : AggregateRoot
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

            public sealed partial class Memento
            {
                public string? NaturalKey { get; set; }

                public string? Name { get; set; }
            }
        }

        public partial class NewSubject
        {
            public string? NaturalKey { get; set; }
        }

        public partial class SubjectRenamed
        {
            public string? Name { get; set; }
        }

        private sealed partial class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
            }
        }
    }

    // A repository with custom storage appends the events itself, inside its own transaction, through AppendEventsAsync.
    public sealed partial class CustomStorageStoresEvents : SqlServerMementoPersistence
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

        public partial class Subject : AggregateRoot
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

        public partial class NewSubject
        {
            public string? NaturalKey { get; set; }
        }

        public sealed partial class SubjectRepository(string connectionString) : SqlServerRepository<Subject>(connectionString)
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

        private sealed partial class BootStrapper : IBootstrap<Subject>
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
    public sealed partial class CustomIdentityMap : SqlServerMementoPersistence
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

        public partial class Subject : AggregateRoot
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

            public sealed partial class Memento
            {
                public string? NaturalKey { get; set; }

                public string? Name { get; set; }
            }
        }

        public partial class NewSubject
        {
            public string? NaturalKey { get; set; }
        }

        public partial class SubjectRenamed
        {
            public string? Name { get; set; }
        }

        // Reads the identity of the stream from the subject's own row. The identity of a new subject is stored
        // when the repository inserts its row, so there is nothing to add or remove here.
        public sealed partial class SubjectIdentityMap(string connectionString) : IIdentityMap
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

        public sealed partial class SubjectRepository(string connectionString, IIdentityMap identityMap) : SqlServerRepository<Subject>(connectionString, identityMap)
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

        private sealed partial class BootStrapper : IBootstrap<Subject>
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
