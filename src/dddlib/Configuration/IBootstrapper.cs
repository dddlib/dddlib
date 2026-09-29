namespace dddlib.Configuration;

/// <summary>
/// Configures the runtime for the domain model in an assembly. There can be at most one bootstrapper per assembly.
/// </summary>
public interface IBootstrapper
{
    void Bootstrap(IConfiguration configure);
}
