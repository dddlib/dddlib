using System.Text.Json;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;

namespace dddlib.Persistence.Memory;

/// <summary>
/// An in-process event store. Events are stored as JSON and deserialized on read so that a loaded aggregate root
/// never shares event instances with the one that was saved, and so that serialization problems surface early. It is
/// also the <see cref="IEventFeed"/> for projections over it. Each event is stored with metadata, as on SQL Server:
/// dddlib's <c>hostname</c> and <c>timestamp</c>, and the entries a <see cref="MemoryRepository{T}"/> adds.
/// </summary>
public sealed class MemoryEventStore : IMultiStreamEventStore, IEventFeed
{
    private static long instances;

    private readonly Lock sync = new();
    private readonly Dictionary<Guid, List<StoredEvent>> streams = [];
    private readonly List<StoredEvent> log = [];
    private long sequenceNumber;

    public Task<StreamResult> GetStreamAsync(Guid streamId, int streamRevision, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(streamRevision);

        lock (this.sync)
        {
            if (!this.streams.TryGetValue(streamId, out var stream))
            {
                return Task.FromResult(new StreamResult([], null));
            }

            var events = stream
                .Skip(streamRevision)
                .Select(static storedEvent => storedEvent.Deserialize())
                .ToArray();

            return Task.FromResult(new StreamResult(events, stream[^1].State));
        }
    }

    public Task<string> CommitStreamAsync(Guid streamId, IReadOnlyList<object> events, Guid correlationId, string? preCommitState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            throw new ArgumentException("At least one event is required.", nameof(events));
        }

        lock (this.sync)
        {
            var conflict = this.FindConflict(streamId, preCommitState);
            if (conflict is not null)
            {
                throw new ConcurrencyException(conflict);
            }

            var state = NewState();
            this.Append(streamId, events, correlationId, state);

            return Task.FromResult(state);
        }
    }

    public Task<IReadOnlyList<string>> CommitStreamsAsync(IReadOnlyList<StreamCommit> commits, Guid correlationId, CancellationToken cancellationToken = default)
    {
        StreamConcurrencyException.Validate(commits);

        var staged = commits.Select(static commit => Stage(commit.Events)).ToArray();

        lock (this.sync)
        {
            // Every state is checked, and every event serialized, before anything is appended, so a failure leaves the
            // store untouched.
            foreach (var commit in commits)
            {
                var conflict = this.FindConflict(commit.StreamId, commit.PreCommitState);
                if (conflict is not null)
                {
                    throw new StreamConcurrencyException(commit.StreamId, conflict);
                }
            }

            var states = new string[commits.Count];
            for (var index = 0; index < commits.Count; index++)
            {
                states[index] = NewState();
                this.Append(commits[index].StreamId, staged[index], correlationId, states[index]);
            }

            return Task.FromResult<IReadOnlyList<string>>(states);
        }
    }

    public Task<EventPage> ReadEventsAsync(long afterSequenceNumber, int maxCount, IReadOnlyCollection<Type>? eventTypes = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequenceNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            var page = this.ReadPage(afterSequenceNumber, maxCount);
            var events = page
                .Where(stored => eventTypes is null || eventTypes.Count == 0 || eventTypes.Contains(stored.Type))
                .Select(static stored => stored.ToFeedEvent())
                .ToArray();

            return Task.FromResult(new EventPage(page.Count == 0 ? afterSequenceNumber : page[^1].SequenceNumber, events));
        }
    }

    public Task<long> GetLastSequenceNumberAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.sync)
        {
            return Task.FromResult(this.sequenceNumber);
        }
    }

    /// <summary>
    /// Reads committed events in sequence order, starting after the specified sequence number. This is the feed
    /// the event dispatcher batches from.
    /// </summary>
    internal IReadOnlyList<FeedEvent> ReadEvents(long afterSequenceNumber, int maxCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        lock (this.sync)
        {
            return this.ReadPage(afterSequenceNumber, maxCount)
                .Select(static stored => stored.ToFeedEvent())
                .ToArray();
        }
    }

    /// <summary>
    /// Gets the lock that guards the store, for a <see cref="MemoryBatch"/> that appends to several streams at once.
    /// </summary>
    internal Lock Sync => this.sync;

    /// <summary>
    /// Gets the order in which a <see cref="MemoryBatch"/> takes this store's lock among the locks of other stores.
    /// </summary>
    internal long BatchOrder { get; } = Interlocked.Increment(ref instances);

    /// <summary>
    /// Serializes events ahead of a commit, with the metadata each is stored with: dddlib's, at one timestamp for them
    /// all, then <paramref name="metadata"/>, with each event's own from <paramref name="eventMetadata"/> merged over it.
    /// An entry that would replace dddlib's is an <see cref="ArgumentException"/>, before anything is stored.
    /// </summary>
    internal static StagedEvents Stage(
        IReadOnlyList<object> events,
        IReadOnlyDictionary<string, string>? metadata = null,
        Func<object, int, IReadOnlyDictionary<string, string>?>? eventMetadata = null)
    {
        EventMetadata.Validate(metadata, nameof(metadata));

        var timestamp = DateTime.UtcNow;
        var staged = new StagedEvent[events.Count];
        for (var index = 0; index < events.Count; index++)
        {
            var @event = events[index];
            staged[index] = new StagedEvent(
                @event.GetType(),
                JsonSerializer.Serialize(@event, @event.GetType(), JsonSerialization.Options),
                EventMetadata.Create(metadata, eventMetadata?.Invoke(@event, index), timestamp));
        }

        return new StagedEvents(staged);
    }

    /// <summary>
    /// Appends staged events on behalf of a memento save, as <see cref="AppendEvents"/> does. The caller holds
    /// <see cref="Sync"/>.
    /// </summary>
    internal void AppendStaged(Guid streamId, StagedEvents staged, Guid correlationId, string state) =>
        this.Append(streamId, staged, correlationId, state);

    private static string NewState() => Guid.NewGuid().ToString("N")[..8];

    // Returns why a commit with the pre-commit state would conflict with the stream, or null when it would not.
    private string? FindConflict(Guid streamId, string? preCommitState)
    {
        if (!this.streams.TryGetValue(streamId, out var stream))
        {
            return preCommitState is null ? null : "Aggregate root does not exist.";
        }

        if (stream[^1].State == preCommitState)
        {
            return null;
        }

        return preCommitState is null ? "Aggregate root already exists." : "A concurrency exception has occurred.";
    }

    // Sequence numbers are contiguous from 1 here (a failed commit consumes none), so the events after N start at
    // index N of the log.
    private List<StoredEvent> ReadPage(long afterSequenceNumber, int maxCount) =>
        this.log
            .Skip((int)Math.Min(afterSequenceNumber, this.log.Count))
            .Take(maxCount)
            .ToList();

    private void Append(Guid streamId, IReadOnlyList<object> events, Guid correlationId, string state) =>
        this.Append(streamId, Stage(events), correlationId, state);

    // The caller holds the lock. Nothing here can fail, so a commit staged in full is applied in full.
    private void Append(Guid streamId, StagedEvents staged, Guid correlationId, string state)
    {
        if (staged.Events.Count == 0)
        {
            return;
        }

        if (!this.streams.TryGetValue(streamId, out var stream))
        {
            stream = [];
            this.streams.Add(streamId, stream);
        }

        foreach (var (type, payload, metadata) in staged.Events)
        {
            var sequenced = new StoredEvent(++this.sequenceNumber, streamId, stream.Count + 1, correlationId, type, payload, metadata, state);
            stream.Add(sequenced);
            this.log.Add(sequenced);
        }
    }

    /// <summary>
    /// Events serialized ahead of a commit, so that a failure to serialize one leaves the store untouched.
    /// </summary>
    internal sealed record StagedEvents(IReadOnlyList<StagedEvent> Events);

    /// <summary>
    /// An event serialized ahead of a commit, with the metadata it is stored with.
    /// </summary>
    internal sealed record StagedEvent(Type Type, string Payload, IReadOnlyDictionary<string, string> Metadata);

    private sealed record StoredEvent(
        long SequenceNumber,
        Guid StreamId,
        int StreamRevision,
        Guid CorrelationId,
        Type Type,
        string Payload,
        IReadOnlyDictionary<string, string> Metadata,
        string State)
    {
        public object Deserialize() => JsonSerializer.Deserialize(this.Payload, this.Type, JsonSerialization.Options)!;

        public FeedEvent ToFeedEvent() => new(this.SequenceNumber, this.StreamId, this.StreamRevision, this.CorrelationId, this.Metadata, this.Deserialize());
    }
}
