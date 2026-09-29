using System.Collections.Concurrent;
using dddlib.Persistence.Sdk;

namespace dddlib.Persistence.Memory;

public sealed class MemorySnapshotStore : ISnapshotStore
{
    private readonly ConcurrentDictionary<Guid, Snapshot> snapshots = new();

    public Task PutSnapshotAsync(Guid streamId, Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        this.snapshots[streamId] = snapshot;
        return Task.CompletedTask;
    }

    public Task<Snapshot?> GetSnapshotAsync(Guid streamId, CancellationToken cancellationToken = default) =>
        Task.FromResult(this.snapshots.TryGetValue(streamId, out var snapshot) ? snapshot : null);
}
