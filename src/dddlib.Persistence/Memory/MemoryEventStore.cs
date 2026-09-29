using System.Text.Json;
using dddlib.Persistence.Sdk;
using dddlib.Sdk;

namespace dddlib.Persistence.Memory;

/// <summary>
/// An in-process event store. Events are stored as JSON and deserialized on read so that a loaded aggregate root
/// never shares event instances with the one that was saved, and so that serialization problems surface early.
/// </summary>
public sealed class MemoryEventStore : IEventStore
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
                .Select(static storedEvent => JsonSerializer.Deserialize(storedEvent.Payload, storedEvent.Type, JsonSerialization.Options)!)
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
            this.Append(streamId, events, state);

            return Task.FromResult(state);
        }
    }

    /// <summary>
    /// Reads committed events in sequence order, starting after the specified sequence number. This is the feed
    /// the event dispatcher batches from.
    /// </summary>
    public Task<IReadOnlyList<SequencedEvent>> ReadEventsAsync(long afterSequenceNumber, int maxCount, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(this.ReadEvents(afterSequenceNumber, maxCount));
    }

    internal IReadOnlyList<SequencedEvent> ReadEvents(long afterSequenceNumber, int maxCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        lock (this.sync)
        {
            return this.log
                .Where(stored => stored.SequenceNumber > afterSequenceNumber)
                .Take(maxCount)
                .Select(static stored => new SequencedEvent(stored.SequenceNumber, JsonSerializer.Deserialize(stored.Payload, stored.Type, JsonSerialization.Options)!))
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
            this.Append(streamId, events, state);
        }
    }

    private void Append(Guid streamId, IReadOnlyList<object> events, string state)
    {
        // Serialize everything first so that a failure leaves the store untouched.
        var stored = events
            .Select(@event => new StoredEvent(0, @event.GetType(), JsonSerializer.Serialize(@event, @event.GetType(), JsonSerialization.Options), state))
            .ToArray();

        if (!this.streams.TryGetValue(streamId, out var stream))
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

    private sealed record StoredEvent(long SequenceNumber, Type Type, string Payload, string State);
}
