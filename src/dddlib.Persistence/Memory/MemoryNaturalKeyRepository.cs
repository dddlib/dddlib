using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Memory;

/// <summary>
/// An in-process natural key repository: an append-only log with a single increasing checkpoint.
/// </summary>
public sealed class MemoryNaturalKeyRepository : INaturalKeyRepository
{
    private readonly Lock sync = new();
    private readonly List<Entry> store = [];
    private long checkpoint;

    public Task<IReadOnlyList<NaturalKeyRecord>> GetNaturalKeysAsync(Type aggregateRootType, long checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);

        lock (this.sync)
        {
            IReadOnlyList<NaturalKeyRecord> records = this.store
                .Where(entry => entry.AggregateRootType == aggregateRootType && entry.Record.Checkpoint > checkpoint)
                .Select(static entry => entry.Record)
                .ToArray();

            return Task.FromResult(records);
        }
    }

    public Task<NaturalKeyRecord?> TryAddNaturalKeyAsync(Type aggregateRootType, string serializedNaturalKey, long checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);
        ArgumentNullException.ThrowIfNull(serializedNaturalKey);

        lock (this.sync)
        {
            var entries = this.store.Where(entry => entry.AggregateRootType == aggregateRootType).ToArray();
            var latestCheckpoint = entries.Length == 0 ? 0L : entries[^1].Record.Checkpoint;
            if (latestCheckpoint != checkpoint)
            {
                return Task.FromResult<NaturalKeyRecord?>(null);
            }

            var isDuplicate = entries
                .GroupBy(static entry => entry.Record.Identity)
                .Select(static group => group.Last().Record)
                .Any(record => !record.IsRemoved && record.SerializedValue == serializedNaturalKey);

            if (isDuplicate)
            {
                return Task.FromResult<NaturalKeyRecord?>(null);
            }

            var record = new NaturalKeyRecord(Guid.NewGuid(), serializedNaturalKey, ++this.checkpoint, IsRemoved: false);
            this.store.Add(new Entry(aggregateRootType, record));

            return Task.FromResult<NaturalKeyRecord?>(record);
        }
    }

    public Task RemoveAsync(Guid naturalKeyIdentity, CancellationToken cancellationToken = default)
    {
        lock (this.sync)
        {
            var latest = this.store.LastOrDefault(entry => entry.Record.Identity == naturalKeyIdentity);
            if (latest is null || latest.Record.IsRemoved)
            {
                return Task.CompletedTask;
            }

            var record = latest.Record with { Checkpoint = ++this.checkpoint, IsRemoved = true };
            this.store.Add(new Entry(latest.AggregateRootType, record));

            return Task.CompletedTask;
        }
    }

    private sealed record Entry(Type AggregateRootType, NaturalKeyRecord Record);
}
