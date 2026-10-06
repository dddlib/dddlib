namespace dddlib.Persistence.Sdk;

/// <summary>
/// Serializes natural keys for storage in a natural key repository.
/// </summary>
public interface INaturalKeySerializer
{
    string Serialize(Type naturalKeyType, object naturalKey);

    object Deserialize(Type naturalKeyType, string serializedNaturalKey);

    /// <summary>
    /// Gets a value indicating whether equal natural keys of the type always serialize to the same text, so that a
    /// repository can tell whether a key is already present by comparing serialized keys. A key that is not, such as
    /// one with a case-insensitive equality comparer, is compared by the identity map with its own equality.
    /// </summary>
    bool IsCanonical(Type naturalKeyType) => false;
}
