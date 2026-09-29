namespace dddlib.Persistence.Sdk;

/// <summary>
/// Serializes natural keys for storage in a natural key repository.
/// </summary>
public interface INaturalKeySerializer
{
    string Serialize(Type naturalKeyType, object naturalKey);

    object Deserialize(Type naturalKeyType, string serializedNaturalKey);
}
