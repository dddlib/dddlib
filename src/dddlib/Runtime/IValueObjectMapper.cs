using System.ComponentModel;
using dddlib.Sdk;

namespace dddlib.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IValueObjectMapper<TValueObject> : IFluentExtensions
    where TValueObject : ValueObject<TValueObject>
{
    T ToEvent<T>();

    T ToEvent<T>(T @event);
}
