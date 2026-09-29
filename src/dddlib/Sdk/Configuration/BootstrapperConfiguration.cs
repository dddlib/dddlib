using System.Linq.Expressions;
using dddlib.Configuration;
using dddlib.Runtime;
using dddlib.Sdk.Configuration.Model;

namespace dddlib.Sdk.Configuration;

/// <summary>
/// The <see cref="IConfiguration"/> handed to a bootstrapper while a single runtime type is being created. Only
/// configuration for that type takes effect; configuration for other types is ignored.
/// </summary>
internal sealed class BootstrapperConfiguration : IConfiguration
{
    private readonly AggregateRootType? aggregateRootType;
    private readonly EntityType? entityType;
    private readonly ValueObjectType? valueObjectType;
    private readonly ITypeAnalyzerService typeAnalyzerService;

    public BootstrapperConfiguration(AggregateRootType aggregateRootType, ITypeAnalyzerService typeAnalyzerService)
    {
        ArgumentNullException.ThrowIfNull(aggregateRootType);
        ArgumentNullException.ThrowIfNull(typeAnalyzerService);

        this.aggregateRootType = aggregateRootType;
        this.entityType = aggregateRootType;
        this.typeAnalyzerService = typeAnalyzerService;
    }

    public BootstrapperConfiguration(EntityType entityType, ITypeAnalyzerService typeAnalyzerService)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(typeAnalyzerService);

        this.entityType = entityType;
        this.typeAnalyzerService = typeAnalyzerService;
    }

    public BootstrapperConfiguration(ValueObjectType valueObjectType, ITypeAnalyzerService typeAnalyzerService)
    {
        ArgumentNullException.ThrowIfNull(valueObjectType);
        ArgumentNullException.ThrowIfNull(typeAnalyzerService);

        this.valueObjectType = valueObjectType;
        this.typeAnalyzerService = typeAnalyzerService;
    }

    public IAggregateRootConfigurationWrapper<T> AggregateRoot<T>()
        where T : AggregateRoot
    {
        if (this.aggregateRootType is not null && this.aggregateRootType.RuntimeType == typeof(T))
        {
            return new AggregateRootConfigurationWrapper<T>(this.aggregateRootType, this.Entity<T>());
        }

        return new EmptyAggregateRootConfigurationWrapper<T>(this.Entity<T>());
    }

    public IEntityConfigurationWrapper<T> Entity<T>()
        where T : Entity
    {
        if (this.entityType is not null && this.entityType.RuntimeType == typeof(T))
        {
            return new EntityConfigurationWrapper<T>(this.entityType, this.typeAnalyzerService);
        }

        return new EmptyEntityConfigurationWrapper<T>();
    }

    public IValueObjectConfigurationWrapper<T> ValueObject<T>()
        where T : notnull
    {
        if (this.valueObjectType is not null && this.valueObjectType.RuntimeType == typeof(T))
        {
            return new ValueObjectConfigurationWrapper<T>(this.valueObjectType);
        }

        return new EmptyValueObjectConfigurationWrapper<T>();
    }

    private sealed class EmptyAggregateRootConfigurationWrapper<T>(IEntityConfigurationWrapper<T> entityConfigurationWrapper)
        : IAggregateRootConfigurationWrapper<T>
        where T : AggregateRoot
    {
        public IAggregateRootConfigurationWrapper<T> ToReconstituteUsing(Func<T> uninitializedFactory) => this;

        public IAggregateRootConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector)
        {
            entityConfigurationWrapper.ToUseNaturalKey(naturalKeySelector);
            return this;
        }
    }

    private sealed class EmptyEntityConfigurationWrapper<T> : IEntityConfigurationWrapper<T>
        where T : Entity
    {
        public IEntityConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector) => this;

        public IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping) => this;

        public IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping) => this;
    }

    private sealed class EmptyValueObjectConfigurationWrapper<T> : IValueObjectConfigurationWrapper<T>
        where T : notnull
    {
        public IValueObjectConfigurationWrapper<T> ToUseValueObjectSerializer(IValueObjectSerializer valueObjectSerializer) => this;

        public IValueObjectConfigurationWrapper<T> ToUseValueObjectSerializer(Func<T, string> serialize, Func<string, T> deserialize) => this;

        public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping) => this;

        public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping) => this;
    }
}
