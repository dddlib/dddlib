using dddlib.Configuration;
using dddlib.Runtime;
using dddlib.Sdk.Configuration.Model;

namespace dddlib.Sdk.Configuration;

internal sealed class ValueObjectConfigurationWrapper<T> : IValueObjectConfigurationWrapper<T>
    where T : ValueObject<T>
{
    private readonly ValueObjectType valueObjectType;

    public ValueObjectConfigurationWrapper(ValueObjectType valueObjectType)
    {
        ArgumentNullException.ThrowIfNull(valueObjectType);

        this.valueObjectType = valueObjectType;
    }

    public IValueObjectConfigurationWrapper<T> ToUseEqualityComparer(IEqualityComparer<T> equalityComparer)
    {
        this.valueObjectType.ConfigureEqualityComparer(equalityComparer);
        return this;
    }

    public IValueObjectConfigurationWrapper<T> ToUseValueObjectSerializer(IValueObjectSerializer valueObjectSerializer)
    {
        this.valueObjectType.ConfigureSerializer(valueObjectSerializer);
        return this;
    }

    public IValueObjectConfigurationWrapper<T> ToUseValueObjectSerializer(Func<T, string> serialize, Func<string, T> deserialize) =>
        this.ToUseValueObjectSerializer(new CustomValueObjectSerializer<T>(serialize, deserialize));

    public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        this.valueObjectType.Mappings.AddOrUpdate(mapping);
        return this;
    }

    public IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(reverseMapping);

        this.valueObjectType.Mappings.AddOrUpdate(mapping);
        this.valueObjectType.Mappings.AddOrUpdate(reverseMapping);
        return this;
    }
}
