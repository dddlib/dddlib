namespace dddlib.Persistence.Sdk;

/// <summary>
/// Maps the natural key of an aggregate root to the identity of its stream.
/// </summary>
public interface IIdentityMap
{
    Task<Guid> GetOrAddAsync(Type aggregateRootType, Type naturalKeyType, object naturalKey, CancellationToken cancellationToken = default);

    Task<Guid?> TryGetAsync(Type aggregateRootType, Type naturalKeyType, object naturalKey, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid identity, CancellationToken cancellationToken = default);
}
