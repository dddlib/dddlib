using dddlib.Runtime;

namespace dddlib.Sdk;

public class CustomValueObjectSerializer<T> : IValueObjectSerializer
    where T : notnull
{
    private readonly Func<T, string> serialize;
    private readonly Func<string, T> deserialize;

    public CustomValueObjectSerializer(Func<T, string> serialize, Func<string, T> deserialize)
    {
        ArgumentNullException.ThrowIfNull(serialize);
        ArgumentNullException.ThrowIfNull(deserialize);

        this.serialize = serialize;
        this.deserialize = deserialize;
    }

    public string Serialize(T valueObject) => this.serialize(valueObject);

    public T Deserialize(string serializedValueObject) => this.deserialize(serializedValueObject);

    string IValueObjectSerializer.Serialize(object valueObject) => this.Serialize((T)valueObject);

    object IValueObjectSerializer.Deserialize(string serializedValueObject) => this.Deserialize(serializedValueObject);
}
