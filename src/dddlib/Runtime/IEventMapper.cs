using System.ComponentModel;
using dddlib.Sdk;

namespace dddlib.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEventMapper<TEvent> : IFluentExtensions
{
    T ToEntity<T>()
        where T : Entity;

    T ToValueObject<T>()
        where T : ValueObject<T>;
}
