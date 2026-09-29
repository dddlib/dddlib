namespace dddlib.Sdk;

public static class TypeExtensions
{
    /// <summary>
    /// Gets the type hierarchy of the source type, from the source type up to but excluding the specified type.
    /// </summary>
    public static IEnumerable<Type> GetTypeHierarchyUntil(this Type sourceType, Type type)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(type);

        var current = sourceType;
        do
        {
            yield return current;
        }
        while ((current = current.BaseType) != type && current is not null);
    }

    /// <summary>
    /// Gets a stable name for the type that does not include the assembly version.
    /// </summary>
    public static string GetSerializedName(this Type sourceType)
    {
        ArgumentNullException.ThrowIfNull(sourceType);

        return string.Concat(sourceType.FullName, ", ", sourceType.Assembly.GetName().Name);
    }
}
