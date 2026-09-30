using System.ComponentModel;
using System.Linq.Expressions;
using dddlib.Sdk;

namespace dddlib.Configuration;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEntityConfigurationWrapper<T> : IFluentExtensions
    where T : Entity
{
    IEntityConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector);

    IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping);

    IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping);

    IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Func<T, TEvent> mapping);

    IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Func<T, TEvent> mapping, Func<TEvent, T> reverseMapping);

    IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Func<T, TEvent, TEvent> mapping);

    IEntityConfigurationWrapper<T> ToMapToEvent<TEvent>(Func<T, TEvent, TEvent> mapping, Func<TEvent, T> reverseMapping);
}
