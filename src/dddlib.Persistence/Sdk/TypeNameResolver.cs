using System.Collections.Concurrent;
using System.Globalization;

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

    /// <summary>
    /// Resolves a stored type name, or throws with the steps to fix a type that no longer resolves.
    /// </summary>
    public static Type ResolveOrThrow(string serializedName) =>
        Resolve(serializedName)
            ?? throw new PersistenceException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"Cannot deserialize into type of '{0}' as that type does not exist in the assembly '{1}' or the assembly is not referenced by the project.
To fix this issue:
- ensure that the assembly '{1}' contains the type '{0}', and
- check that the assembly '{1}' is referenced by the project.
Further information: https://github.com/dddlib/dddlib/blob/main/docs/persistence/serialization.md",
                    serializedName.Split(',').First().Trim(),
                    serializedName.Split(',').Last().Trim()));
}
