using dddlib.Configuration;

namespace dddlib.Sdk.Configuration;

/// <summary>
/// Locates the bootstrapper that configures a runtime type.
/// </summary>
public interface IBootstrapperProvider
{
    Action<IConfiguration> GetBootstrapper(Type type);
}
