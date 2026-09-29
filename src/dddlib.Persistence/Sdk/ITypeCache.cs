namespace dddlib.Persistence.Sdk;

/// <summary>
/// Maps types to the compact identifiers they are stored under.
/// </summary>
public interface ITypeCache
{
    Task<int> GetTypeIdAsync(Type type, CancellationToken cancellationToken = default);

    Task<string> GetTypeNameAsync(int typeId, CancellationToken cancellationToken = default);

    Task<Type> GetTypeAsync(int typeId, CancellationToken cancellationToken = default);
}
