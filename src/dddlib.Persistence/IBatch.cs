namespace dddlib.Persistence;

/// <summary>
/// Aggregate roots, of any types, to save in one transaction through their own memento repositories: every aggregate
/// root is saved, or none is and no aggregate root changes. Repositories add to a batch with
/// <see cref="Sdk.Repository{T}.Add(IBatch, T)"/>; the storage creates it, for example a
/// <see cref="Memory.MemoryBatch"/>. A batch is used once.
/// </summary>
public interface IBatch
{
    /// <summary>
    /// Saves the aggregate roots added to the batch. Their mementos and events are taken now, not when they were added.
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken = default);
}
