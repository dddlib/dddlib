using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using dddlib.Runtime;
using dddlib.Sdk;
using dddlib.Sdk.Generated;

namespace dddlib.Persistence.Sdk;

/// <summary>
/// Serializes value object natural keys through the value object's configured serializer and everything else
/// (strings, GUIDs, numbers) as JSON.
/// </summary>
public sealed class DefaultNaturalKeySerializer : INaturalKeySerializer
{
    // Types whose equal values always serialize to the same JSON. Not decimal (1.0m equals 1.00m), floating point
    // (0.0 equals -0.0), DateTime (equality ignores the kind) or DateTimeOffset (equality ignores the offset).
    private static readonly HashSet<Type> CanonicalTypes =
    [
        typeof(string), typeof(bool), typeof(char), typeof(Guid),
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
    ];

    private readonly ConcurrentDictionary<Type, bool> canonical = new();

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

    /// <summary>
    /// Gets a value indicating whether equal natural keys of the type always serialize to the same text, and unequal
    /// ones to different text: a string, an integer, a GUID, a Boolean, a character or an enumeration, or a sealed
    /// value object with the default equality comparer and serializer whose properties are all of such types.
    /// </summary>
    public bool IsCanonical(Type naturalKeyType)
    {
        ArgumentNullException.ThrowIfNull(naturalKeyType);

        return this.canonical.GetOrAdd(naturalKeyType, static type => IsCanonical(type, []));
    }

    private static bool IsCanonical(Type type, HashSet<Type> visiting)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (CanonicalTypes.Contains(type) || type.IsEnum)
        {
            return true;
        }

        // a value object that contains itself is not worth following
        if (!type.IsSealed || !type.IsSubclassOfRawGeneric(typeof(ValueObject<>)) || !visiting.Add(type))
        {
            return false;
        }

        var valueObjectType = Application.Current.GetValueObjectType(type);
        var hasDefaultEquality = valueObjectType.EqualityComparer is IGeneratedValueObjectMetadata ||
            valueObjectType.EqualityComparer.GetType().IsGenericType && valueObjectType.EqualityComparer.GetType().GetGenericTypeDefinition() == typeof(DefaultValueObjectEqualityComparer<>);
        var hasDefaultSerializer = valueObjectType.Serializer.GetType().IsGenericType &&
            valueObjectType.Serializer.GetType().GetGenericTypeDefinition() == typeof(DefaultValueObjectSerializer<>);

        // the default comparer compares exactly the properties the default serializer writes, unless an attribute
        // changes what is written
        return hasDefaultEquality &&
            hasDefaultSerializer &&
            type.GetCustomAttribute<JsonConverterAttribute>() is null &&
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0)
                .All(property =>
                    property.GetCustomAttribute<JsonIgnoreAttribute>() is null &&
                    property.GetCustomAttribute<JsonConverterAttribute>() is null &&
                    IsCanonical(property.PropertyType, visiting));
    }
}
