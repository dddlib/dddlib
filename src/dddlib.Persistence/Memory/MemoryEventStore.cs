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

                stream = [];
                this.streams.Add(streamId, stream);
            }
            else if (stream[^1].State != preCommitState)
            {
                throw preCommitState is null
                    ? new ConcurrencyException("Aggregate root already exists.")
                    : new ConcurrencyException();
            }

            var state = string.Empty;
            foreach (var @event in events)
            {
                state = Guid.NewGuid().ToString("N")[..8];
                stream.Add(new StoredEvent(@event.GetType(), JsonSerializer.Serialize(@event, @event.GetType(), JsonSerialization.Options), state));
            }

            return Task.FromResult(state);
        }
    }

    private sealed record StoredEvent(Type Type, string Payload, string State);
}
