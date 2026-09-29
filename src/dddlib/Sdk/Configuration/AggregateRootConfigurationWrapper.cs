using System.Linq.Expressions;
using dddlib.Configuration;
using dddlib.Sdk.Configuration.Model;

namespace dddlib.Sdk.Configuration;

internal sealed class AggregateRootConfigurationWrapper<T> : IAggregateRootConfigurationWrapper<T>
    where T : AggregateRoot
{
    private readonly AggregateRootType aggregateRootType;
    private readonly IEntityConfigurationWrapper<T> entityConfigurationWrapper;

    public AggregateRootConfigurationWrapper(AggregateRootType aggregateRootType, IEntityConfigurationWrapper<T> entityConfigurationWrapper)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);
        ArgumentNullException.ThrowIfNull(entityConfigurationWrapper);

        this.aggregateRootType = aggregateRootType;
        this.entityConfigurationWrapper = entityConfigurationWrapper;
    }

    public IAggregateRootConfigurationWrapper<T> ToReconstituteUsing(Func<T> uninitializedFactory)
    {
        this.aggregateRootType.ConfigureUninitializedFactory(uninitializedFactory);
        return this;
    }

    public IAggregateRootConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
    {
        this.entityConfigurationWrapper.ToUseNaturalKey(naturalKeySelector);
        return this;
    }
}
