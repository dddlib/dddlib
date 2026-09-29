using System.Linq.Expressions;
using dddlib.Configuration;
using dddlib.Sdk.Configuration.Model;

namespace dddlib.Sdk.Configuration;

internal sealed class EntityConfigurationWrapper<T> : IEntityConfigurationWrapper<T>
    where T : Entity
{
    private readonly EntityType entityType;
    private readonly ITypeAnalyzerService typeAnalyzerService;

    public EntityConfigurationWrapper(EntityType entityType, ITypeAnalyzerService typeAnalyzerService)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(typeAnalyzerService);

        this.entityType = entityType;
        this.typeAnalyzerService = typeAnalyzerService;
    }

    public IEntityConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
    {
        ArgumentNullException.ThrowIfNull(naturalKeySelector);

        if (naturalKeySelector.Body is not MemberExpression memberExpression)
        {
            throw new ArgumentException("Value must be a member expression.", nameof(naturalKeySelector));
        }

        var naturalKey = new NaturalKey(typeof(T), memberExpression.Member.Name, typeof(TKey), this.typeAnalyzerService);
        this.entityType.ConfigureNaturalKey(naturalKey);
        return this;
    }

    public IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        this.entityType.Mappings.AddOrUpdate(mapping);
        return this;
    }

    public IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(reverseMapping);

        this.entityType.Mappings.AddOrUpdate(mapping);
        this.entityType.Mappings.AddOrUpdate(reverseMapping);
        return this;
    }
}
