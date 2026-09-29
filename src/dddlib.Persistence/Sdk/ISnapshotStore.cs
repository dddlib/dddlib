namespace dddlib.Persistence.Sdk;

public interface ISnapshotStore
{
    Task PutSnapshotAsync(Guid streamId, Snapshot snapshot, CancellationToken cancellationToken = default);

    Task<Snapshot?> GetSnapshotAsync(Guid streamId, CancellationToken cancellationToken = default);
}
