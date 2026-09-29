using System.ComponentModel;
using dddlib.Sdk;

namespace dddlib.Configuration;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IConfiguration : IFluentExtensions
{
    IAggregateRootConfigurationWrapper<T> AggregateRoot<T>()
        where T : AggregateRoot;

    IEntityConfigurationWrapper<T> Entity<T>()
        where T : Entity;

    IValueObjectConfigurationWrapper<T> ValueObject<T>()
        where T : notnull;
}
