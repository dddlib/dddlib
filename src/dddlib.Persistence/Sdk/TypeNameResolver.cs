using System.Collections.Concurrent;

namespace dddlib.Persistence.Sdk;

/// <summary>
/// Resolves the stable type names written by <see cref="dddlib.Sdk.TypeExtensions.GetSerializedName"/>
/// ("Namespace.Type, AssemblyName") against the assemblies loaded in the process, without depending on assembly
/// versions.
/// </summary>
internal static class TypeNameResolver
{
    private static readonly ConcurrentDictionary<string, Type?> ResolvedTypes = new();

    public static Type? Resolve(string serializedName)
    {
        ArgumentException.ThrowIfNullOrEmpty(serializedName);

        return ResolvedTypes.GetOrAdd(serializedName, static name =>
        {
            var separator = name.LastIndexOf(',');
            if (separator < 0)
            {
                return Type.GetType(name, throwOnError: false);
            }

            var typeName = name[..separator].Trim();
            var assemblyName = name[(separator + 1)..].Trim();

            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                .FirstOrDefault(static candidate => candidate is not null);

            return type ?? Type.GetType(name, throwOnError: false);
        });
    }
}
