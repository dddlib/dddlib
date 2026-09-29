using System.ComponentModel;
using dddlib.Sdk;

namespace dddlib.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IValueObjectMapper<TValueObject> : IFluentExtensions
    where TValueObject : notnull
{
    T ToEvent<T>()
        where T : new();

    T ToEvent<T>(T @event);
}
