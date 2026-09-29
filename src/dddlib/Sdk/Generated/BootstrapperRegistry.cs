using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace dddlib.Sdk.Generated;

/// <summary>
/// The bootstrapper of each assembly, registered at module initialization by the dddlib source generator so that
/// the runtime does not have to scan the assembly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class BootstrapperRegistry
{
    private static readonly ConcurrentDictionary<Assembly, Type?> Registrations = new();

    public static void Register(Type bootstrapperType)
    {
        ArgumentNullException.ThrowIfNull(bootstrapperType);

        Registrations[bootstrapperType.Assembly] = bootstrapperType;
    }

    /// <summary>
    /// Records that the assembly was inspected at compile time and declares no bootstrapper.
    /// </summary>
    public static void RegisterNone(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        Registrations.TryAdd(assembly, null);
    }

    public static bool TryGet(Assembly assembly, out Type? bootstrapperType)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return Registrations.TryGetValue(assembly, out bootstrapperType);
    }
}
