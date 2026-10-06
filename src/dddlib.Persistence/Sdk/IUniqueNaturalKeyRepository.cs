namespace dddlib.Persistence.Sdk;

/// <summary>
/// A natural key repository that keeps natural keys unique itself, by comparing serialized keys, so that adding a key
/// takes a single call and never has to be retried because another key was added first.
/// </summary>
/// <remarks>
/// The identity map uses it only for natural keys whose serializer reports them as canonical
/// (<see cref="INaturalKeySerializer.IsCanonical"/>), for which comparing serialized keys is the same as comparing keys.
/// </remarks>
public interface IUniqueNaturalKeyRepository : INaturalKeyRepository
{
    /// <summary>
    /// Gets the record of the natural key that is present for the aggregate root type with the same serialized value,
    /// or adds one at the next checkpoint.
    /// </summary>
    Task<NaturalKeyRecord> GetOrAddNaturalKeyAsync(Type aggregateRootType, string serializedNaturalKey, CancellationToken cancellationToken = default);
}
