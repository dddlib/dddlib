using System.ComponentModel;
using dddlib.Runtime;
using dddlib.Sdk;

namespace dddlib.Configuration;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IValueObjectConfigurationWrapper<T> : IFluentExtensions
    where T : ValueObject<T>
{
    IValueObjectConfigurationWrapper<T> ToUseEqualityComparer(IEqualityComparer<T> equalityComparer);

    IValueObjectConfigurationWrapper<T> ToUseValueObjectSerializer(IValueObjectSerializer valueObjectSerializer);

    IValueObjectConfigurationWrapper<T> ToUseValueObjectSerializer(Func<T, string> serialize, Func<string, T> deserialize);

    IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping);

    IValueObjectConfigurationWrapper<T> ToMapToEvent<TEvent>(Action<T, TEvent> mapping, Func<TEvent, T> reverseMapping);
}
