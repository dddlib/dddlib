using dddlib.Persistence.Projections.SqlServer;
using dddlib.Persistence.SqlServer;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Projections.Tests.Features;

// As someone who uses dddlib with event sourcing
// In order to serve read models from SQL Server
// I need a projection over the SQL Server event store to keep its views up to date, applying each event exactly once
public abstract class SqlServerProjection : SqlServerFeature
{
    protected static ProjectionRunnerOptions Options { get; } = Configure();

    protected IEventStoreRepository Repository => new SqlServerEventStoreRepository(this.ConnectionString);

    protected SqlServerEventStore Feed => new(this.ConnectionString);

    protected static ProjectionRunnerOptions Configure(int batchSize = 10, TimeSpan? retryDelay = null) => new()
    {
        BatchSize = batchSize,
        PollingInterval = TimeSpan.FromMilliseconds(20),
        MaxPollingInterval = TimeSpan.FromMilliseconds(100),
        RetryDelay = retryDelay ?? TimeSpan.FromMilliseconds(50),
    };

    protected static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!await condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    protected static Task WaitUntilAsync(Func<bool> condition) => WaitUntilAsync(() => Task.FromResult(condition()));

    protected async Task<Subject> SaveAsync(string id, string? name = null)
    {
        var subject = new Subject(id);
        if (name is not null)
        {
            subject.Rename(name);
        }

        await this.Repository.SaveAsync(subject);
        return subject;
    }

    public sealed class CatchesUpFromTheStart : SqlServerProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given three saved aggregate roots
            await this.SaveAsync("a", "Alpha");
            await this.SaveAsync("b");
            await this.SaveAsync("c");

            // When a projection over the event store is run
            var store = new SqlServerProjectionStore<string, SubjectView>(this.ConnectionString, new SubjectProjection());
            await using var runner = new ProjectionRunner(this.Feed, store, Options);
            runner.Start();
            await WaitUntilAsync(() => runner.Checkpoint == 4);

            // Then its views reflect every event, in order, and can be read through a repository of their own
            var views = new SqlServerRepository<string, SubjectView>(this.ConnectionString, "subjects");
            await Assert.That(await views.GetAsync("a")).IsEqualTo(new SubjectView("a", "Alpha", 2));
            await Assert.That(await views.GetAsync("b")).IsEqualTo(new SubjectView("b", null, 1));
            await Assert.That(await views.GetAsync("c")).IsEqualTo(new SubjectView("c", null, 1));
            await Assert.That((await runner.GetStatusAsync()).Lag).IsEqualTo(0);
        }
    }

    public sealed class ResumesFromTheCheckpoint : SqlServerProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a projection that has caught up and stopped
            await this.SaveAsync("a");
            await this.SaveAsync("b");
            var projection = new SubjectProjection();
            var store = new SqlServerProjectionStore<string, SubjectView>(this.ConnectionString, projection);
            await using (var first = new ProjectionRunner(this.Feed, store, Options))
            {
                first.Start();
                await WaitUntilAsync(() => first.Checkpoint == 2);
            }

            // When more events are saved and a new runner takes over the same projection
            await this.SaveAsync("c");
            await using var second = new ProjectionRunner(this.Feed, new SqlServerProjectionStore<string, SubjectView>(this.ConnectionString, projection), Options);
            second.Start();
            await WaitUntilAsync(() => second.Checkpoint == 3);

            // Then only the new event is applied
            await Assert.That(projection.Applied).IsEquivalentTo([1L, 2L, 3L]);
            await Assert.That(await store.Views.GetAsync("c")).IsNotNull();
        }
    }

    public sealed class SkipsEventsItDoesNotHandle : SqlServerProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given events of a type the projection does not handle among the ones it does
            await this.SaveAsync("a", "Alpha");
            await this.SaveAsync("b", "Beta");

            // When a projection handling only the creation event is run
            var projection = new NewSubjectsOnlyProjection();
            var store = new SqlServerProjectionStore<string, SubjectView>(this.ConnectionString, projection);
            await using var runner = new ProjectionRunner(this.Feed, store, Options);
            runner.Start();
            await WaitUntilAsync(() => runner.Checkpoint == 4);

            // Then its checkpoint passes the other events and only the handled ones reach it
            await Assert.That(projection.Applied).IsEquivalentTo([1L, 3L]);
            await Assert.That(await store.Views.GetAsync("a")).IsEqualTo(new SubjectView("a", null, 1));
        }
    }

    public sealed class FailedBatchChangesNothing : SqlServerProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a projection that fails on one of the events in a page, after writing another view
            await this.SaveAsync("a");
            await this.SaveAsync("poison");
            var projection = new SubjectProjection { FailOn = "poison" };
            var store = new SqlServerProjectionStore<string, SubjectView>(this.ConnectionString, projection);
            var failures = new List<Exception>();
            await using var runner = new ProjectionRunner(this.Feed, store, Configure(retryDelay: TimeSpan.FromMinutes(1)));
            runner.ProjectionFailed += (_, e) => failures.Add(e.Exception);

            // When it runs
            runner.Start();
            await WaitUntilAsync(() => failures.Count == 1);

            // Then nothing of the page reaches the views or the checkpoint, and the failure names the event
            await Assert.That(await store.Views.GetAsync("a")).IsNull();
            await Assert.That(await store.GetCheckpointAsync()).IsEqualTo(0);
            var failure = (ProjectionException)failures[0];
            await Assert.That(failure.SequenceNumber).IsEqualTo(2);
            await Assert.That(failure.Event).IsTypeOf<NewSubject>();
        }
    }

    public sealed class FailedBatchIsRetriedInOrder : SqlServerProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a projection that fails once, on the second event
            await this.SaveAsync("a");
            await this.SaveAsync("b");
            await this.SaveAsync("c");
            var projection = new SubjectProjection { FailOn = "b", FailOnce = true };
            var store = new SqlServerProjectionStore<string, SubjectView>(this.ConnectionString, projection);
            var failures = 0;
            await using var runner = new ProjectionRunner(this.Feed, store, Options);
            runner.ProjectionFailed += (_, _) => failures++;

            // When it runs
            runner.Start();
            await WaitUntilAsync(() => runner.Checkpoint == 3);

            // Then the page is applied again from its start, in order, after one failure
            await Assert.That(failures).IsEqualTo(1);
            await Assert.That(projection.Applied).IsEquivalentTo([1L, 2L, 1L, 2L, 3L]);
            await Assert.That((await store.Views.GetAllAsync().ToListAsync()).Select(static view => view.Key).Order()).IsEquivalentTo(["a", "b", "c"]);
        }
    }

    public sealed class RebuildStartsFromZero : SqlServerProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a projection that has caught up and stopped
            await this.SaveAsync("a");
            await this.SaveAsync("b");
            var projection = new SubjectProjection();
            var store = new SqlServerProjectionStore<string, SubjectView>(this.ConnectionString, projection);
            await using (var first = new ProjectionRunner(this.Feed, store, Options))
            {
                first.Start();
                await WaitUntilAsync(() => first.Checkpoint == 2);
            }

            // When it is purged
            await store.PurgeAsync();

            // Then its views and checkpoint are cleared together
            await Assert.That(await store.Views.GetAllAsync().CountAsync()).IsEqualTo(0);
            await Assert.That(await store.GetCheckpointAsync()).IsEqualTo(0);

            // And a runner rebuilds it from the first event
            await using var second = new ProjectionRunner(this.Feed, store, Options);
            second.Start();
            await WaitUntilAsync(() => projection.Applied.Count == 4 && second.Checkpoint == 2);
            await Assert.That(projection.Applied).IsEquivalentTo([1L, 2L, 1L, 2L]);
            await Assert.That(await store.Views.GetAllAsync().CountAsync()).IsEqualTo(2);

            // And a purge while that runner is idle is noticed without an event having to arrive
            await store.PurgeAsync();
            await WaitUntilAsync(() => projection.Applied.Count == 6);
            await WaitUntilAsync(async () => await store.GetCheckpointAsync() == 2);
            await Assert.That(projection.Applied).IsEquivalentTo([1L, 2L, 1L, 2L, 1L, 2L]);
        }
    }

    public sealed class ConcurrentRunnersApplyEachEventOnce : SqlServerProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a counting projection and many events
            for (var index = 0; index < 30; index++)
            {
                await this.SaveAsync($"subject-{index}", "Name");
            }

            var projection = new SubjectProjection();

            // When three runners, each with its own store over the same projection, run at once
            await using var first = new ProjectionRunner(this.Feed, new SqlServerProjectionStore<string, SubjectView>(this.ConnectionString, projection), Configure(batchSize: 7));
            await using var second = new ProjectionRunner(this.Feed, new SqlServerProjectionStore<string, SubjectView>(this.ConnectionString, projection), Configure(batchSize: 3));
            await using var third = new ProjectionRunner(this.Feed, new SqlServerProjectionStore<string, SubjectView>(this.ConnectionString, projection), Configure(batchSize: 11));
            first.Start();
            second.Start();
            third.Start();
            var views = new SqlServerRepository<string, SubjectView>(this.ConnectionString, projection.Name);
            await WaitUntilAsync(async () => await views.GetAllAsync().CountAsync() == 30 && (await views.GetAllAsync().ToListAsync()).All(static view => view.Value.Events == 2));

            // Then every event was applied exactly once and the checkpoint is at the end
            var all = await views.GetAllAsync().ToListAsync();
            await Assert.That(all).Count().IsEqualTo(30);
            await Assert.That(all.All(static view => view.Value.Events == 2)).IsTrue();
            await Assert.That(await first.GetStatusAsync()).IsEqualTo(new ProjectionStatus(projection.Name, 60, 60));
        }
    }
}
