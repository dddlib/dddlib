using dddlib.Runtime;

namespace dddlib.Sdk;

internal sealed class DefaultEntityMapper<TEntity> : IEntityMapper<TEntity>
    where TEntity : Entity
{
    private readonly TEntity source;

    public DefaultEntityMapper(TEntity source)
    {
        ArgumentNullException.ThrowIfNull(source);

        this.source = source;
    }

    public T ToEvent<T>() =>
        EventMapping.ToNewEvent<TEntity, T>(this.Mappings, this.source, EventMapping.Entity);

    public T ToEvent<T>(T @event) =>
        EventMapping.ToEvent(this.Mappings, this.source, @event, EventMapping.Entity);

    private MapperCollection Mappings => Application.Current.GetEntityType(this.source.GetType()).Mappings;
}
