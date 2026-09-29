using dddlib.Sdk.Configuration.Model;

namespace dddlib.Sdk.Configuration;

internal sealed class EntityTypeFactory
{
    private readonly ITypeAnalyzerService typeAnalyzerService;
    private readonly IBootstrapperProvider bootstrapperProvider;

    public EntityTypeFactory(ITypeAnalyzerService typeAnalyzerService, IBootstrapperProvider bootstrapperProvider)
    {
        ArgumentNullException.ThrowIfNull(typeAnalyzerService);
        ArgumentNullException.ThrowIfNull(bootstrapperProvider);

        this.typeAnalyzerService = typeAnalyzerService;
        this.bootstrapperProvider = bootstrapperProvider;
    }

    public EntityType Create(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var entityType = default(EntityType);
        foreach (var subType in type.GetTypeHierarchyUntil(typeof(object)).Reverse())
        {
            entityType = entityType is null
                ? new EntityType(subType, this.typeAnalyzerService)
                : new EntityType(subType, this.typeAnalyzerService, entityType);
        }

        var configuration = new BootstrapperConfiguration(entityType!, this.typeAnalyzerService);
        var bootstrapper = this.bootstrapperProvider.GetBootstrapper(type);
        bootstrapper(configuration);

        return entityType!;
    }
}
