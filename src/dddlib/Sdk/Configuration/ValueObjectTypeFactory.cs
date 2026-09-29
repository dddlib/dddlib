using dddlib.Sdk.Configuration.Model;

namespace dddlib.Sdk.Configuration;

internal sealed class ValueObjectTypeFactory
{
    private readonly ITypeAnalyzerService typeAnalyzerService;
    private readonly IBootstrapperProvider bootstrapperProvider;

    public ValueObjectTypeFactory(ITypeAnalyzerService typeAnalyzerService, IBootstrapperProvider bootstrapperProvider)
    {
        ArgumentNullException.ThrowIfNull(typeAnalyzerService);
        ArgumentNullException.ThrowIfNull(bootstrapperProvider);

        this.typeAnalyzerService = typeAnalyzerService;
        this.bootstrapperProvider = bootstrapperProvider;
    }

    public ValueObjectType Create(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var valueObjectType = new ValueObjectType(type, this.typeAnalyzerService);
        var configuration = new BootstrapperConfiguration(valueObjectType, this.typeAnalyzerService);
        var bootstrapper = this.bootstrapperProvider.GetBootstrapper(type);
        bootstrapper(configuration);

        return valueObjectType;
    }
}
