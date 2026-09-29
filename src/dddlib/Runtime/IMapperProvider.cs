using dddlib.Sdk;

namespace dddlib.Runtime;

/// <summary>
/// Provides mappers between entities, value objects and events, as configured in a bootstrapper.
/// </summary>
public interface IMapperProvider : IFluentExtensions
{
    IEventMapper<T> Event<T>(T @event)
        where T : notnull;

    IEntityMapper<T> Entity<T>(T entity)
        where T : Entity;

    IValueObjectMapper<T> ValueObject<T>(T valueObject)
        where T : ValueObject<T>;
}
