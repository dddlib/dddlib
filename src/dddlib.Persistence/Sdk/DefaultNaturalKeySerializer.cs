using System.Text.Json;
using dddlib.Runtime;
using dddlib.Sdk;

namespace dddlib.Persistence.Sdk;

/// <summary>
/// Serializes value object natural keys through the value object's configured serializer and everything else
/// (strings, GUIDs, numbers) as JSON.
/// </summary>
public sealed class DefaultNaturalKeySerializer : INaturalKeySerializer
{
    public string Serialize(Type naturalKeyType, object naturalKey)
    {
        ArgumentNullException.ThrowIfNull(naturalKeyType);
        ArgumentNullException.ThrowIfNull(naturalKey);

        if (naturalKeyType.IsSubclassOfRawGeneric(typeof(ValueObject<>)))
        {
            return Application.Current.GetValueObjectType(naturalKeyType).Serializer.Serialize(naturalKey);
        }

        return JsonSerializer.Serialize(naturalKey, naturalKeyType, JsonSerialization.Options);
    }

    public object Deserialize(Type naturalKeyType, string serializedNaturalKey)
    {
        ArgumentNullException.ThrowIfNull(naturalKeyType);
        ArgumentNullException.ThrowIfNull(serializedNaturalKey);

        if (naturalKeyType.IsSubclassOfRawGeneric(typeof(ValueObject<>)))
        {
            return Application.Current.GetValueObjectType(naturalKeyType).Serializer.Deserialize(serializedNaturalKey);
        }

        return JsonSerializer.Deserialize(serializedNaturalKey, naturalKeyType, JsonSerialization.Options)
            ?? throw new PersistenceException("The serialized natural key deserialized to null.");
    }
}
