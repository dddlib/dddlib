using System.Data;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;
using Microsoft.Data.SqlClient;

namespace dddlib.Persistence.Projections.Tests.Features;

// As someone who uses dddlib with event sourcing
// In order to serve read models from tables of my own design
// I need a projection whose handlers write to my tables in the transaction that moves its checkpoint
public abstract class SqlServerCustomTableProjection : SqlServerFeature
{
    private const string CreateTable = """
        CREATE TABLE [dbo].[SubjectNames]
        (
            [Id] NVARCHAR(100) NOT NULL PRIMARY KEY,
            [Name] NVARCHAR(100) NULL,
            [Events] INT NOT NULL
        );
        """;

    protected static ProjectionRunnerOptions Options { get; } = new()
    {
        BatchSize = 10,
        PollingInterval = TimeSpan.FromMilliseconds(20),
        MaxPollingInterval = TimeSpan.FromMilliseconds(100),
        RetryDelay = TimeSpan.FromMinutes(1),
    };

    protected IEventStoreRepository Repository => new SqlServerEventStoreRepository(this.ConnectionString);

    protected SqlServerEventStore Feed => new(this.ConnectionString);

    [Before(Test)]
    public Task CreateTableAsync() => this.Database.ExecuteScriptAsync(CreateTable);

    [After(Test)]
    public Task DropTableAsync() => this.Database.ExecuteScriptAsync("DROP TABLE [dbo].[SubjectNames];");

    protected static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!await condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    protected async Task SaveAsync(string id, string? name = null)
    {
        var subject = new Subject(id);
        if (name is not null)
        {
            subject.Rename(name);
        }

        await this.Repository.SaveAsync(subject);
    }

    protected async Task<List<(string Id, string? Name, int Events)>> ReadTableAsync()
    {
        var rows = new List<(string, string?, int)>();
        await using var connection = new SqlConnection(this.ConnectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT [Id], [Name], [Events] FROM [dbo].[SubjectNames] ORDER BY [Id];";
        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetInt32(2)));
        }

        return rows;
    }

    public sealed class WritesToTheUsersTables : SqlServerCustomTableProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given saved aggregate roots and a projection into a table of the user's
            await this.SaveAsync("a", "Alpha");
            await this.SaveAsync("b");
            var projection = new SubjectTableProjection(this.ConnectionString);

            // When it runs
            await using var runner = new ProjectionRunner(this.Feed, projection, Options);
            runner.Start();
            await WaitUntilAsync(async () => await projection.GetCheckpointAsync() == 3);

            // Then the table reflects every event
            await Assert.That(await this.ReadTableAsync()).IsEquivalentTo([("a", (string?)"Alpha", 2), ("b", null, 1)]);
        }
    }

    public sealed class FailedBatchRollsBackTheUsersTables : SqlServerCustomTableProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a projection that fails on the second event of a page, after writing the first
            await this.SaveAsync("a");
            await this.SaveAsync("poison");
            var projection = new SubjectTableProjection(this.ConnectionString) { FailOn = "poison" };
            var failures = new List<Exception>();
            await using var runner = new ProjectionRunner(this.Feed, projection, Options);
            runner.ProjectionFailed += (_, e) => failures.Add(e.Exception);

            // When it runs
            runner.Start();
            await WaitUntilAsync(() => Task.FromResult(failures.Count == 1));

            // Then the user's table and the checkpoint are untouched
            await Assert.That(await this.ReadTableAsync()).IsEmpty();
            await Assert.That(await projection.GetCheckpointAsync()).IsEqualTo(0);
            await Assert.That(((ProjectionException)failures[0]).SequenceNumber).IsEqualTo(2);
        }
    }

    public sealed class RebuildPurgesTheUsersTables : SqlServerCustomTableProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a projection that has caught up
            await this.SaveAsync("a", "Alpha");
            var projection = new SubjectTableProjection(this.ConnectionString);
            await using var runner = new ProjectionRunner(this.Feed, projection, Options);
            runner.Start();
            await WaitUntilAsync(async () => await projection.GetCheckpointAsync() == 2);

            // When it is purged
            await projection.PurgeAsync();
            var rowsAfterPurge = await this.ReadTableAsync();

            // Then the user's table was cleared through the projection, and is rebuilt from the first event
            await WaitUntilAsync(async () => await projection.GetCheckpointAsync() == 2 && projection.Purged == 1);
            await Assert.That(rowsAfterPurge).IsEmpty();
            await WaitUntilAsync(async () => (await this.ReadTableAsync()).Count == 1);
            await Assert.That(await this.ReadTableAsync()).IsEquivalentTo([("a", (string?)"Alpha", 2)]);
        }
    }

    public sealed class SubjectTableProjection : SqlServer.SqlServerProjection
    {
        public SubjectTableProjection(string connectionString)
            : base(connectionString, "subject-names")
        {
            this.When<NewSubject>(async (@event, transaction, cancellationToken) =>
            {
                if (@event.Id == this.FailOn)
                {
                    throw new InvalidOperationException("poisoned");
                }

                await ExecuteAsync(transaction, "INSERT INTO [dbo].[SubjectNames] ([Id], [Name], [Events]) VALUES (@Id, NULL, 1);", @event.Id!, null, cancellationToken);
            });

            this.When<SubjectRenamed>((@event, transaction, cancellationToken) =>
                ExecuteAsync(transaction, "UPDATE [dbo].[SubjectNames] SET [Name] = @Name, [Events] = [Events] + 1 WHERE [Id] = @Id;", @event.Id!, @event.Name, cancellationToken));
        }

        public string? FailOn { get; init; }

        public int Purged { get; private set; }

        protected override async Task PurgeAsync(SqlTransaction transaction, CancellationToken cancellationToken)
        {
            this.Purged++;
            await ExecuteAsync(transaction, "DELETE FROM [dbo].[SubjectNames];", null, null, cancellationToken);
        }

        private static async Task ExecuteAsync(SqlTransaction transaction, string commandText, string? id, string? name, CancellationToken cancellationToken)
        {
            await using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = commandText;
            command.Parameters.Add("@Id", SqlDbType.NVarChar, 100).Value = (object?)id ?? DBNull.Value;
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = (object?)name ?? DBNull.Value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
