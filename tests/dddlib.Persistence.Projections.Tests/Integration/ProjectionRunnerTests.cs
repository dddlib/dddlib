using dddlib.Persistence.Projections.Sdk;
using dddlib.Persistence.Sdk;
using Microsoft.Extensions.Time.Testing;

namespace dddlib.Persistence.Projections.Tests.Integration;

// The runner's loop over a fake feed and store, with a fake clock for the delays.
public class ProjectionRunnerTests
{
    private static readonly ProjectionRunnerOptions Options = new()
    {
        BatchSize = 10,
        PollingInterval = TimeSpan.FromSeconds(1),
        MaxPollingInterval = TimeSpan.FromSeconds(4),
        RetryDelay = TimeSpan.FromSeconds(30),
    };

    [Test]
    public async Task BacksOffWhenIdle()
    {
        var clock = new Clock();
        var feed = new FakeFeed();
        await using var runner = new ProjectionRunner(feed, new FakeStore(), Options, clock);

        // Each poll finds no events and waits on the clock; the clock is advanced only once the runner is waiting.
        runner.Start();
        await WaitUntilAsync(() => clock.Timers == 1);
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntilAsync(() => clock.Timers == 2);
        clock.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(100);
        var afterOneSecond = feed.Reads;
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntilAsync(() => clock.Timers == 3);

        // 1s, 2s, 4s, 4s: capped at the maximum.
        clock.Advance(TimeSpan.FromSeconds(4));
        await WaitUntilAsync(() => clock.Timers == 4);
        clock.Advance(TimeSpan.FromSeconds(4));
        await WaitUntilAsync(() => feed.Reads == 5);

        await Assert.That(afterOneSecond).IsEqualTo(2);
    }

    [Test]
    public async Task RetriesAFailedPageAfterTheDelay()
    {
        var clock = new Clock();
        var feed = new FakeFeed();
        feed.Append(new Event(1));
        var store = new FakeStore { FailuresLeft = 1 };
        var failures = new List<Exception>();
        await using var runner = new ProjectionRunner(feed, store, Options, clock);
        runner.ProjectionFailed += (_, e) => failures.Add(e.Exception);

        runner.Start();
        await WaitUntilAsync(() => clock.Timers == 1);
        await Task.Delay(100);
        var appliedBeforeTheDelay = store.Applied.Count;
        clock.Advance(TimeSpan.FromSeconds(30));
        await WaitUntilAsync(() => store.Applied.Count == 1);

        await Assert.That(appliedBeforeTheDelay).IsEqualTo(0);
        await Assert.That(failures).HasSingleItem();
        await Assert.That(failures[0]).IsTypeOf<InvalidOperationException>();
        await Assert.That(store.Checkpoint).IsEqualTo(1);
        await Assert.That(runner.Checkpoint).IsEqualTo(1);
    }

    [Test]
    public async Task ALostRaceIsNotAFailure()
    {
        var clock = new FakeTimeProvider();
        var feed = new FakeFeed();
        feed.Append(new Event(1));
        feed.Append(new Event(2));
        var store = new FakeStore { LoseRacesLeft = 1, Checkpoint = 1 };
        var failures = 0;
        await using var runner = new ProjectionRunner(feed, store, Options, clock);
        runner.ProjectionFailed += (_, _) => failures++;

        // The store reports checkpoint 0 to the first read, loses the race (another runner moved it to 1), and the
        // runner carries on from 1 without a delay.
        store.ReportedCheckpoints.Enqueue(0);
        runner.Start();
        await WaitUntilAsync(() => store.Applied.Count == 1);

        await Assert.That(failures).IsEqualTo(0);
        await Assert.That(store.Applied[0].Events.Select(static e => e.SequenceNumber)).IsEquivalentTo([2L]);
        await Assert.That(store.Checkpoint).IsEqualTo(2);
    }

    [Test]
    public async Task RejectsAProjectionWithoutHandlers()
    {
        var store = new FakeStore { EventTypes = [] };

        await Assert.That(() => new ProjectionRunner(new FakeFeed(), store))
            .Throws<ArgumentException>()
            .WithMessageContaining("The projection 'fake' handles no events");
    }

    [Test]
    public async Task ReportsItsStatus()
    {
        var feed = new FakeFeed();
        for (var id = 1; id <= 10; id++)
        {
            feed.Append(new Event(id));
        }

        await using var runner = new ProjectionRunner(feed, new FakeStore { Checkpoint = 3 }, Options);

        var status = await runner.GetStatusAsync();

        await Assert.That(status).IsEqualTo(new ProjectionStatus("fake", 3, 10));
        await Assert.That(status.Lag).IsEqualTo(7);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    /// <summary>
    /// A fake clock that counts the timers created on it: the runner creates one for each delay, so a test can tell
    /// that the runner is waiting before it advances the clock.
    /// </summary>
    private sealed class Clock : FakeTimeProvider
    {
        private int timers;

        public int Timers => Volatile.Read(ref this.timers);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Interlocked.Increment(ref this.timers);
            return base.CreateTimer(callback, state, dueTime, period);
        }
    }

    private sealed record Event(int Id);

    private sealed class FakeFeed : IEventFeed
    {
        private readonly List<FeedEvent> events = [];

        public int Reads { get; private set; }

        public void Append(object @event) =>
            this.events.Add(new FeedEvent(this.events.Count + 1, Guid.Empty, this.events.Count + 1, Guid.Empty, @event));

        public async Task<EventPage> ReadEventsAsync(long afterSequenceNumber, int maxCount, IReadOnlyCollection<Type>? eventTypes = null, CancellationToken cancellationToken = default)
        {
            this.Reads++;

            // a read takes time, as it does against a store, so the test cannot rely on the runner being ahead of it
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);

            var page = this.events.Where(e => e.SequenceNumber > afterSequenceNumber).Take(maxCount).ToList();
            return new EventPage(page.Count == 0 ? afterSequenceNumber : page[^1].SequenceNumber, page);
        }

        public Task<long> GetLastSequenceNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult((long)this.events.Count);
    }

    private sealed class FakeStore : IProjectionStore
    {
        public string Name => "fake";

        public IReadOnlyCollection<Type>? EventTypes { get; init; } = [typeof(Event)];

        public long Checkpoint { get; set; }

        public Queue<long> ReportedCheckpoints { get; } = new();

        public int FailuresLeft { get; set; }

        public int LoseRacesLeft { get; set; }

        public List<EventPage> Applied { get; } = [];

        public Task<long> GetCheckpointAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(this.ReportedCheckpoints.TryDequeue(out var reported) ? reported : this.Checkpoint);

        public Task ApplyAsync(EventPage page, long expectedCheckpoint, CancellationToken cancellationToken = default)
        {
            if (this.LoseRacesLeft-- > 0)
            {
                throw new ConcurrencyException();
            }

            if (this.FailuresLeft-- > 0)
            {
                throw new InvalidOperationException("transient");
            }

            if (expectedCheckpoint != this.Checkpoint)
            {
                throw new ConcurrencyException();
            }

            this.Applied.Add(page);
            this.Checkpoint = page.EndSequenceNumber;
            return Task.CompletedTask;
        }

        public Task PurgeAsync(CancellationToken cancellationToken = default)
        {
            this.Checkpoint = 0;
            return Task.CompletedTask;
        }
    }
}
