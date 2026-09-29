namespace dddlib.Persistence.Sdk;

/// <summary>
/// The durable log of natural keys behind an identity map. Records are appended with an increasing checkpoint;
/// removals are appended as records flagged <see cref="NaturalKeyRecord.IsRemoved"/>.
/// </summary>
public interface INaturalKeyRepository
{
    /// <summary>
    /// Gets the records for an aggregate root type with a checkpoint greater than the specified one, in checkpoint order.
    /// </summary>
    Task<IReadOnlyList<NaturalKeyRecord>> GetNaturalKeysAsync(Type aggregateRootType, long checkpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a natural key if <paramref name="checkpoint"/> is the latest checkpoint for the aggregate root type and
    /// the key is not already present. Returns null when the caller must synchronize and retry.
    /// </summary>
    Task<NaturalKeyRecord?> TryAddNaturalKeyAsync(Type aggregateRootType, string serializedNaturalKey, long checkpoint, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid naturalKeyIdentity, CancellationToken cancellationToken = default);
}
