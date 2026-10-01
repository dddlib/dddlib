using System.Text.Json;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;

namespace dddlib.Persistence.Memory;

/// <summary>
/// An in-process event store. Events are stored as JSON and deserialized on read so that a loaded aggregate root
/// never shares event instances with the one that was saved, and so that serialization problems surface early. It is
/// also the <see cref="IEventFeed"/> for projections over it.
/// </summary>
public sealed class MemoryEventStore : IEventStore, IEventFeed
{
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
            if (!this.streams.TryGetValue(streamId, out var stream))
            {
                if (preCommitState is not null)
                {
                    throw new ConcurrencyException("Aggregate root does not exist.");
                }
            }
            else if (stream[^1].State != preCommitState)
            {
                throw preCommitState is null
                    ? new ConcurrencyException("Aggregate root already exists.")
                    : new ConcurrencyException();
            }

            var state = Guid.NewGuid().ToString("N")[..8];
            this.Append(streamId, events, correlationId, state);

            return Task.FromResult(state);
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
                .Select(static stored => new FeedEvent(stored.SequenceNumber, stored.StreamId, stored.StreamRevision, stored.CorrelationId, stored.Deserialize()))
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
    internal IReadOnlyList<SequencedEvent> ReadEvents(long afterSequenceNumber, int maxCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        lock (this.sync)
        {
            return this.ReadPage(afterSequenceNumber, maxCount)
                .Select(static stored => new SequencedEvent(stored.SequenceNumber, stored.Deserialize()))
                .ToArray();
        }
    }

    /// <summary>
    /// Appends events to a stream on behalf of a memento save and sets the stream's state token to the token the
    /// memento was saved with. The memento's token is authoritative for concurrency, so there is no state check here.
    /// </summary>
    internal void AppendEvents(Guid streamId, IReadOnlyList<object> events, string state)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentException.ThrowIfNullOrEmpty(state);

        if (events.Count == 0)
        {
            return;
        }

        lock (this.sync)
        {
            this.Append(streamId, events, Guid.NewGuid(), state);
        }
    }

    // Sequence numbers are contiguous from 1 here (a failed commit consumes none), so the events after N start at
    // index N of the log.
    private List<StoredEvent> ReadPage(long afterSequenceNumber, int maxCount) =>
        this.log
            .Skip((int)Math.Min(afterSequenceNumber, this.log.Count))
            .Take(maxCount)
            .ToList();

    private void Append(Guid streamId, IReadOnlyList<object> events, Guid correlationId, string state)
    {
        var revision = this.streams.TryGetValue(streamId, out var stream) ? stream.Count : 0;

        // Serialize everything first so that a failure leaves the store untouched.
        var stored = events
            .Select((@event, index) => new StoredEvent(
                0,
                streamId,
                revision + index + 1,
                correlationId,
                @event.GetType(),
                JsonSerializer.Serialize(@event, @event.GetType(), JsonSerialization.Options),
                state))
            .ToArray();

        if (stream is null)
        {
            stream = [];
            this.streams.Add(streamId, stream);
        }

        foreach (var storedEvent in stored)
        {
            var sequenced = storedEvent with { SequenceNumber = ++this.sequenceNumber };
            stream.Add(sequenced);
            this.log.Add(sequenced);
        }
    }

    private sealed record StoredEvent(long SequenceNumber, Guid StreamId, int StreamRevision, Guid CorrelationId, Type Type, string Payload, string State)
    {
        public object Deserialize() => JsonSerializer.Deserialize(this.Payload, this.Type, JsonSerialization.Options)!;
    }
}
