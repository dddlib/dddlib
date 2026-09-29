using System.ComponentModel;
using dddlib.Sdk;

namespace dddlib.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEntityMapper<TEntity> : IFluentExtensions
    where TEntity : Entity
{
    T ToEvent<T>()
        where T : new();

    T ToEvent<T>(T @event);
}
