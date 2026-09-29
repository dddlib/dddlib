using System.Globalization;
using dddlib.Configuration;
using dddlib.Runtime;
using dddlib.Sdk.Generated;

namespace dddlib.Sdk.Configuration;

/// <summary>
/// Locates the single <see cref="IBootstrapper"/> implementation in the assembly that declares a type.
/// </summary>
public class DefaultBootstrapperProvider : IBootstrapperProvider
{
    public Action<IConfiguration> GetBootstrapper(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        // The source generator registers each assembly's bootstrapper at module initialization; scanning is the fallback.
        var bootstrapperTypes = BootstrapperRegistry.TryGet(type.Assembly, out var registered)
            ? registered is null ? [] : [registered]
            : type.Assembly.GetTypes()
                .Where(static assemblyType => assemblyType.IsClass && !assemblyType.IsAbstract && typeof(IBootstrapper).IsAssignableFrom(assemblyType))
                .ToArray();

        if (bootstrapperTypes.Length == 0)
        {
            return static _ => { };
        }

        if (bootstrapperTypes.Length > 1)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The assembly '{0}' has more than one bootstrapper defined. There can only be a single bootstrapper defined per assembly.
To fix this issue:
- ensure that there is only a single instance of a bootstrapper class declared in the assembly.",
                    type.Assembly.GetName()))
            {
                HelpLink = "https://github.com/dddlib/dddlib/wiki/Bootstrapper",
            };
        }

        var bootstrapperType = bootstrapperTypes[0];
        if (bootstrapperType.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The bootstrapper of type '{0}' cannot be instantiated as it does not have a default constructor.
To fix this issue:
- add a default constructor to the bootstrapper.",
                    bootstrapperType))
            {
                HelpLink = "https://github.com/dddlib/dddlib/wiki/Bootstrapper",
            };
        }

        IBootstrapper bootstrapper;
        try
        {
            bootstrapper = (IBootstrapper)Activator.CreateInstance(bootstrapperType)!;
        }
        catch (RuntimeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The bootstrapper of type '{0}' threw an exception during instantiation.\r\nSee inner exception for details.",
                    bootstrapperType),
                ex);
        }

        return bootstrapper.Bootstrap;
    }
}
