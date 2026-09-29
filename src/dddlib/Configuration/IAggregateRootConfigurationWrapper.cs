using System.ComponentModel;
using System.Linq.Expressions;
using dddlib.Sdk;

namespace dddlib.Configuration;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IAggregateRootConfigurationWrapper<T> : IFluentExtensions
    where T : AggregateRoot
{
    IAggregateRootConfigurationWrapper<T> ToReconstituteUsing(Func<T> uninitializedFactory);

    IAggregateRootConfigurationWrapper<T> ToUseNaturalKey<TKey>(Expression<Func<T, TKey>> naturalKeySelector);
}
