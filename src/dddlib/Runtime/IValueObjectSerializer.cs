namespace dddlib.Runtime;

/// <summary>
/// Serializes value objects to and from strings, for example for natural key storage.
/// </summary>
public interface IValueObjectSerializer
{
    string Serialize(object valueObject);

    object Deserialize(string serializedValueObject);
}
