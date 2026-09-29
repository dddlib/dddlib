using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace dddlib.Sdk.Generated;

/// <summary>
/// Locates the metadata the dddlib source generator emits into partial domain types. The lookup happens once per
/// type when its runtime metadata is built; the hot paths then use the generated code directly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedMetadata
{
    /// <summary>
    /// The name of the nested class the generator emits.
    /// </summary>
    public const string TypeName = "__DddlibMetadata";

    private static readonly ConcurrentDictionary<Type, object?> Instances = new();

    public static TMetadata? TryGet<TMetadata>(Type type)
        where TMetadata : class
    {
        ArgumentNullException.ThrowIfNull(type);

        return Instances.GetOrAdd(type, static type => Create(type)) as TMetadata;
    }

    private static object? Create(Type type)
    {
        var metadataType = type.GetNestedType(TypeName, BindingFlags.Public | BindingFlags.NonPublic);
        return metadataType is null ? null : Activator.CreateInstance(metadataType, nonPublic: true);
    }
}
