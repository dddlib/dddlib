using System.Globalization;
using System.Text.Json;
using dddlib.Runtime;

namespace dddlib.Sdk;

/// <summary>
/// Serializes value objects as JSON using System.Text.Json. Records with a positional constructor and types with
/// a parameterless constructor and settable properties round-trip without configuration.
/// </summary>
public sealed class DefaultValueObjectSerializer<T> : IValueObjectSerializer
    where T : ValueObject<T>
{
    public string Serialize(object valueObject)
    {
        ArgumentNullException.ThrowIfNull(valueObject);

        return JsonSerializer.Serialize(valueObject, valueObject.GetType(), JsonSerialization.Options);
    }

    public object Deserialize(string serializedValueObject)
    {
        ArgumentNullException.ThrowIfNull(serializedValueObject);

        try
        {
            return JsonSerializer.Deserialize<T>(serializedValueObject, JsonSerialization.Options)
                ?? throw new JsonException("The serialized value object is null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"Unable to deserialize value object of type '{0}' using the default value object serializer.
To fix this issue, either:
- ensure the value object has a single public constructor whose parameters match its public properties, or a public parameterless constructor and settable properties, or
- define a custom serializer in a bootstrapper.",
                    typeof(T)),
                ex)
            {
                HelpLink = "https://github.com/dddlib/dddlib/blob/main/docs/value-object-serialization.md",
            };
        }
    }
}
