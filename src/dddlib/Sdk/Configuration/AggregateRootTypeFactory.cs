using dddlib.Sdk.Configuration.Model;

namespace dddlib.Sdk.Configuration;

internal sealed class AggregateRootTypeFactory
{
    private readonly ITypeAnalyzerService typeAnalyzerService;
    private readonly IBootstrapperProvider bootstrapperProvider;

    public AggregateRootTypeFactory(ITypeAnalyzerService typeAnalyzerService, IBootstrapperProvider bootstrapperProvider)
    {
        ArgumentNullException.ThrowIfNull(typeAnalyzerService);
        ArgumentNullException.ThrowIfNull(bootstrapperProvider);

        this.typeAnalyzerService = typeAnalyzerService;
        this.bootstrapperProvider = bootstrapperProvider;
    }

    public AggregateRootType Create(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        // The entity part of the hierarchy: from Entity up to but excluding the first aggregate root type.
        var entityType = default(EntityType);
        foreach (var subType in type.GetTypeHierarchyUntil(typeof(object)).Reverse())
        {
            if (entityType is null)
            {
                entityType = new EntityType(subType, this.typeAnalyzerService);
                continue;
            }

            if (this.typeAnalyzerService.IsValidAggregateRoot(subType))
            {
                break;
            }

            entityType = new EntityType(subType, this.typeAnalyzerService, entityType);
        }

        // The aggregate root part of the hierarchy: from AggregateRoot down to the type itself.
        var aggregateRootType = default(AggregateRootType);
        foreach (var subType in type.GetTypeHierarchyUntil(typeof(Entity)).Reverse())
        {
            aggregateRootType = new AggregateRootType(subType, this.typeAnalyzerService, aggregateRootType ?? entityType!);
        }

        var configuration = new BootstrapperConfiguration(aggregateRootType!, this.typeAnalyzerService);
        var bootstrapper = this.bootstrapperProvider.GetBootstrapper(type);
        bootstrapper(configuration);

        return aggregateRootType!;
    }
}
