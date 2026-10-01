using dddlib.Configuration;
using dddlib.Persistence.Memory;
using dddlib.Persistence.Projections.Memory;
using dddlib.Persistence.Sdk;
using dddlib.Tests.Support;

namespace dddlib.Persistence.Projections.Tests.Features;

// As someone who uses dddlib with event sourcing
// In order to serve read models
// I need a projection over the in-memory event store to keep its views up to date, applying each event exactly once
public abstract class MemoryProjection : Feature
{
    protected MemoryProjection()
    {
        this.EventStore = new MemoryEventStore();
        this.Repository = new EventStoreRepository(new MemoryIdentityMap(), this.EventStore, new MemorySnapshotStore());
    }

    protected static ProjectionRunnerOptions Options { get; } = Configure();

    protected static ProjectionRunnerOptions Configure(int batchSize = 10, TimeSpan? retryDelay = null) => new()
    {
        BatchSize = batchSize,
        PollingInterval = TimeSpan.FromMilliseconds(20),
        MaxPollingInterval = TimeSpan.FromMilliseconds(100),
        RetryDelay = retryDelay ?? TimeSpan.FromMilliseconds(50),
    };

    protected MemoryEventStore EventStore { get; }

    protected IEventStoreRepository Repository { get; }

    protected static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

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

    public sealed class CatchesUpFromTheStart : MemoryProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given three saved aggregate roots
            await this.SaveAsync("a", "Alpha");
            await this.SaveAsync("b");
            await this.SaveAsync("c");

            // When a projection over the event store is run
            var store = new MemoryProjectionStore<string, SubjectView>(new SubjectProjection());
            await using var runner = new ProjectionRunner(this.EventStore, store, Options);
            runner.Start();
            await WaitUntilAsync(() => runner.Checkpoint == 4);

            // Then its views reflect every event, in order
            await Assert.That(await store.Views.GetAsync("a")).IsEqualTo(new SubjectView("a", "Alpha", 2));
            await Assert.That(await store.Views.GetAsync("b")).IsEqualTo(new SubjectView("b", null, 1));
            await Assert.That(await store.Views.GetAsync("c")).IsEqualTo(new SubjectView("c", null, 1));
            await Assert.That((await runner.GetStatusAsync()).Lag).IsEqualTo(0);
        }
    }

    public sealed class ResumesFromTheCheckpoint : MemoryProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a projection that has caught up and stopped
            await this.SaveAsync("a");
            await this.SaveAsync("b");
            var projection = new SubjectProjection();
            var store = new MemoryProjectionStore<string, SubjectView>(projection);
            await using (var first = new ProjectionRunner(this.EventStore, store, Options))
            {
                first.Start();
                await WaitUntilAsync(() => first.Checkpoint == 2);
            }

            // When more events are saved and a new runner takes over the same store
            await this.SaveAsync("c");
            await using var second = new ProjectionRunner(this.EventStore, store, Options);
            second.Start();
            await WaitUntilAsync(() => second.Checkpoint == 3);

            // Then only the new event is applied
            await Assert.That(projection.Applied).IsEquivalentTo([1L, 2L, 3L]);
            await Assert.That(await store.Views.GetAsync("c")).IsNotNull();
        }
    }

    public sealed class SkipsEventsItDoesNotHandle : MemoryProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given events of a type the projection does not handle among the ones it does
            await this.SaveAsync("a", "Alpha");
            await this.SaveAsync("b", "Beta");

            // When a projection handling only the creation event is run
            var projection = new NewSubjectsOnlyProjection();
            var store = new MemoryProjectionStore<string, SubjectView>(projection);
            await using var runner = new ProjectionRunner(this.EventStore, store, Options);
            runner.Start();
            await WaitUntilAsync(() => runner.Checkpoint == 4);

            // Then its checkpoint passes the other events and only the handled ones reach it
            await Assert.That(projection.Applied).IsEquivalentTo([1L, 3L]);
            await Assert.That(await store.Views.GetAsync("a")).IsEqualTo(new SubjectView("a", null, 1));
        }
    }

    public sealed class FailedBatchChangesNothing : MemoryProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a projection that fails on one of the events in a page, after writing another view
            await this.SaveAsync("a");
            await this.SaveAsync("poison");
            var projection = new SubjectProjection { FailOn = "poison" };
            var store = new MemoryProjectionStore<string, SubjectView>(projection);
            var failures = new List<Exception>();
            await using var runner = new ProjectionRunner(this.EventStore, store, Configure(retryDelay: TimeSpan.FromMinutes(1)));
            runner.ProjectionFailed += (_, e) => failures.Add(e.Exception);

            // When it runs
            runner.Start();
            await WaitUntilAsync(() => failures.Count == 1);

            // Then nothing of the page reaches the views or the checkpoint, and the failure names the event
            await Assert.That(await store.Views.GetAsync("a")).IsNull();
            await Assert.That(await store.GetCheckpointAsync()).IsEqualTo(0);
            var failure = (ProjectionException)failures[0];
            await Assert.That(failure.ProjectionName).IsEqualTo("subjects");
            await Assert.That(failure.SequenceNumber).IsEqualTo(2);
            await Assert.That(failure.Event).IsTypeOf<NewSubject>();
            await Assert.That(failure.InnerException).IsTypeOf<InvalidOperationException>();
        }
    }

    public sealed class FailedBatchIsRetriedInOrder : MemoryProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a projection that fails once, on the second event
            await this.SaveAsync("a");
            await this.SaveAsync("b");
            await this.SaveAsync("c");
            var projection = new SubjectProjection { FailOn = "b", FailOnce = true };
            var store = new MemoryProjectionStore<string, SubjectView>(projection);
            var failures = 0;
            await using var runner = new ProjectionRunner(this.EventStore, store, Options);
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

    public sealed class RebuildStartsFromZero : MemoryProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a projection that has caught up
            await this.SaveAsync("a");
            await this.SaveAsync("b");
            var projection = new SubjectProjection();
            var store = new MemoryProjectionStore<string, SubjectView>(projection);
            await using var runner = new ProjectionRunner(this.EventStore, store, Options);
            runner.Start();
            await WaitUntilAsync(() => runner.Checkpoint == 2);

            // When it is purged
            await store.PurgeAsync();
            var viewsAfterPurge = await store.Views.GetAllAsync().CountAsync();
            var checkpointAfterPurge = await store.GetCheckpointAsync();

            // Then the running runner rebuilds it from the first event
            await WaitUntilAsync(() => projection.Applied.Count == 4);
            await WaitUntilAsync(() => store.Views.GetAllAsync().CountAsync().AsTask().Result == 2);
            await Assert.That(viewsAfterPurge).IsEqualTo(0);
            await Assert.That(checkpointAfterPurge).IsEqualTo(0);
            await Assert.That(projection.Applied).IsEquivalentTo([1L, 2L, 1L, 2L]);
            await Assert.That(await store.GetCheckpointAsync()).IsEqualTo(2);
        }
    }

    public sealed class ConcurrentRunnersApplyEachEventOnce : MemoryProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a counting projection and many events
            for (var index = 0; index < 50; index++)
            {
                await this.SaveAsync($"subject-{index % 5}-{index}", "Name");
            }

            var store = new MemoryProjectionStore<string, SubjectView>(new SubjectProjection());

            // When three runners share the store
            await using var first = new ProjectionRunner(this.EventStore, store, Configure(batchSize: 7));
            await using var second = new ProjectionRunner(this.EventStore, store, Configure(batchSize: 3));
            await using var third = new ProjectionRunner(this.EventStore, store, Configure(batchSize: 11));
            first.Start();
            second.Start();
            third.Start();
            await WaitUntilAsync(() => store.GetCheckpointAsync().Result == 100);

            // Then every event was applied exactly once
            var views = await store.Views.GetAllAsync().ToListAsync();
            await Assert.That(views).Count().IsEqualTo(50);
            await Assert.That(views.All(static view => view.Value.Events == 2)).IsTrue();
        }
    }

    public sealed class CatchAllHandlerReceivesEveryEvent : MemoryProjection
    {
        [Test]
        public async Task Scenario()
        {
            // Given a projection with a catch-all beside an exact handler
            await this.SaveAsync("a", "Alpha");
            var projection = new CatchAllProjection();
            var store = new MemoryProjectionStore<string, SubjectView>(projection);

            // When it runs
            await using var runner = new ProjectionRunner(this.EventStore, store, Options);
            runner.Start();
            await WaitUntilAsync(() => runner.Checkpoint == 2);

            // Then the feed is not filtered, the exact handler takes its event and the catch-all the rest
            await Assert.That(projection.EventTypes).IsNull();
            await Assert.That(projection.Exact).IsEquivalentTo([1L]);
            await Assert.That(projection.CaughtAll).IsEquivalentTo([2L]);
        }
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

        public void Rename(string name) => this.Apply(new SubjectRenamed { Id = this.Id, Name = name });

        private void Handle(NewSubject @event) => this.Id = @event.Id;

        private void Handle(SubjectRenamed @event) => this.Name = @event.Name;
    }

    public class NewSubject
    {
        public string? Id { get; set; }
    }

    public class SubjectRenamed
    {
        public string? Id { get; set; }

        public string? Name { get; set; }
    }

    public sealed record SubjectView(string Id, string? Name, int Events);

    public sealed class SubjectProjection : Projection<string, SubjectView>
    {
        public SubjectProjection()
            : base("subjects")
        {
            this.When<NewSubject>(async (envelope, @event, views, cancellationToken) =>
            {
                this.Applied.Add(envelope.SequenceNumber);
                this.ThrowIfPoisoned(@event.Id!);
                await views.AddOrUpdateAsync(@event.Id!, new SubjectView(@event.Id!, null, 1), cancellationToken);
            });

            this.When<SubjectRenamed>(async (envelope, @event, views, cancellationToken) =>
            {
                this.Applied.Add(envelope.SequenceNumber);
                var view = await views.GetAsync(@event.Id!, cancellationToken) ?? throw new InvalidOperationException("The view must exist.");
                await views.AddOrUpdateAsync(@event.Id!, view with { Name = @event.Name, Events = view.Events + 1 }, cancellationToken);
            });
        }

        public List<long> Applied { get; } = [];

        public string? FailOn { get; init; }

        public bool FailOnce { get; init; }

        private bool failed;

        private void ThrowIfPoisoned(string id)
        {
            if (id == this.FailOn && !(this.FailOnce && this.failed))
            {
                this.failed = true;
                throw new InvalidOperationException("poisoned");
            }
        }
    }

    public sealed class NewSubjectsOnlyProjection : Projection<string, SubjectView>
    {
        public NewSubjectsOnlyProjection()
            : base("new-subjects")
        {
            this.When<NewSubject>((envelope, @event, views, cancellationToken) =>
            {
                this.Applied.Add(envelope.SequenceNumber);
                return views.AddOrUpdateAsync(@event.Id!, new SubjectView(@event.Id!, null, 1), cancellationToken);
            });
        }

        public List<long> Applied { get; } = [];
    }

    public sealed class CatchAllProjection : Projection<string, SubjectView>
    {
        public CatchAllProjection()
            : base("everything")
        {
            this.When<NewSubject>((envelope, _, _, _) =>
            {
                this.Exact.Add(envelope.SequenceNumber);
                return Task.CompletedTask;
            });

            this.When<object>((envelope, _, _, _) =>
            {
                this.CaughtAll.Add(envelope.SequenceNumber);
                return Task.CompletedTask;
            });
        }

        public List<long> Exact { get; } = [];

        public List<long> CaughtAll { get; } = [];
    }

    private sealed class BootStrapper : IBootstrap<Subject>
    {
        public void Bootstrap(IConfiguration configure)
        {
            configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
        }
    }
}
