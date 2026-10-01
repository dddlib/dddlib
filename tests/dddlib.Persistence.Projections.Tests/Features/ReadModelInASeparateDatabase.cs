using dddlib.Persistence.Projections.SqlServer;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Projections.Tests.Features;

// As someone who uses dddlib with event sourcing
// In order to keep a read model away from the event store
// I need the runner to read events from one database and write views and the checkpoint to another
public abstract class ReadModelInASeparateDatabase : SqlServerFeature
{
    [ClassDataSource<SqlServerDatabase>(Shared = SharedType.None)]
    public required SqlServerDatabase ReadModel { get; init; }

    public sealed class Scenario : ReadModelInASeparateDatabase
    {
        [Test]
        public async Task TheRunnerReadsOneDatabaseAndWritesAnother()
        {
            // Given an event store in one database and the schema installed in a second
            var repository = new SqlServerEventStoreRepository(this.ConnectionString);
            await repository.SaveAsync(new Subject("a"));
            await SqlServerProjectionsSchema.EnsureAsync(this.ReadModel.ConnectionString);

            // When a projection reads the first and writes the second
            var store = new SqlServerProjectionStore<string, SubjectView>(this.ReadModel.ConnectionString, new SubjectProjection());
            await using var runner = new ProjectionRunner(
                new SqlServerEventStore(this.ConnectionString),
                store,
                new ProjectionRunnerOptions { PollingInterval = TimeSpan.FromMilliseconds(20) });
            runner.Start();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (runner.Checkpoint != 1)
            {
                await Task.Delay(20, timeout.Token);
            }

            // Then the views and the checkpoint are in the second database only
            await Assert.That(await store.Views.GetAsync("a")).IsEqualTo(new SubjectView("a", null, 1));
            await Assert.That(await this.ReadModel.ExecuteScalarAsync("SELECT [Checkpoint] FROM [dbo].[Projections] WHERE [Name] = 'subjects';")).IsEqualTo(1L);
            await Assert.That(await this.ReadModel.ExecuteScalarAsync("SELECT COUNT(*) FROM [dbo].[Events];")).IsEqualTo(0);
            await Assert.That(await this.Database.ExecuteScalarAsync("SELECT COUNT(*) FROM [dbo].[Projections];")).IsEqualTo(0);
        }
    }
}
