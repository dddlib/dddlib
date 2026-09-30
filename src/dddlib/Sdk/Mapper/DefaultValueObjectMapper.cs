using dddlib.Runtime;

namespace dddlib.Sdk;

internal sealed class DefaultValueObjectMapper<TValueObject> : IValueObjectMapper<TValueObject>
    where TValueObject : ValueObject<TValueObject>
{
    private readonly TValueObject source;

    public DefaultValueObjectMapper(TValueObject source)
    {
        ArgumentNullException.ThrowIfNull(source);

        this.source = source;
    }

    public T ToEvent<T>() =>
        EventMapping.ToNewEvent<TValueObject, T>(this.Mappings, this.source, EventMapping.ValueObject);

    public T ToEvent<T>(T @event) =>
        EventMapping.ToEvent(this.Mappings, this.source, @event, EventMapping.ValueObject);

    private MapperCollection Mappings => Application.Current.GetValueObjectType(this.source.GetType()).Mappings;
}
