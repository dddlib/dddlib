using dddlib.Runtime;

namespace dddlib.Sdk;

public sealed class DefaultMapperProvider : IMapperProvider
{
    public IEventMapper<T> Event<T>(T @event)
        where T : notnull
        => new DefaultEventMapper<T>(@event);

    public IEntityMapper<T> Entity<T>(T entity)
        where T : Entity
        => new DefaultEntityMapper<T>(entity);

    public IValueObjectMapper<T> ValueObject<T>(T valueObject)
        where T : ValueObject<T>
        => new DefaultValueObjectMapper<T>(valueObject);
}
