using System.ComponentModel;

namespace dddlib.Sdk.Generated;

/// <summary>
/// Metadata about an entity type emitted by the dddlib source generator into a nested class named
/// <see cref="GeneratedMetadata.TypeName"/>. Describes only what the type itself declares; base types carry their own.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedEntityMetadata
{
    /// <summary>
    /// Gets the name of the property marked with <see cref="NaturalKeyAttribute"/> on this type, or null.
    /// </summary>
    string? NaturalKeyPropertyName { get; }

    Type? NaturalKeyPropertyType { get; }

    object? GetNaturalKeyValue(Entity entity);
}
