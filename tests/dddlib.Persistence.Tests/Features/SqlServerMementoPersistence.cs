using System.Data;
using dddlib.Configuration;
using dddlib.Persistence.Sdk;
using dddlib.Persistence.SqlServer;
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
}
