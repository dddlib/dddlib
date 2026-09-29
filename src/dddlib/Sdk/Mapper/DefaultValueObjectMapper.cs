using System.Globalization;
using dddlib.Runtime;

namespace dddlib.Sdk;

internal sealed class DefaultValueObjectMapper<TValueObject> : IValueObjectMapper<TValueObject>
    where TValueObject : notnull
{
    private readonly TValueObject source;

    public DefaultValueObjectMapper(TValueObject source)
    {
        ArgumentNullException.ThrowIfNull(source);

        this.source = source;
    }

    public T ToEvent<T>()
        where T : new()
        => this.ToEvent(new T());

    public T ToEvent<T>(T @event)
    {
        var runtimeType = Application.Current.GetValueObjectType(this.source.GetType());
        if (!runtimeType.Mappings.TryGet<TValueObject, T>(out Action<TValueObject, T>? mapping))
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The value object of type '{0}' has not been configured to map to an event of type '{1}'.
To fix this issue:
- use a bootstrapper to register a mapping for the event.",
                    this.source.GetType(),
                    typeof(T)))
            {
                HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Value-Object-Mapping",
            };
        }

        try
        {
            mapping(this.source, @event);
        }
        catch (RuntimeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "An exception occurred whilst attempting to map a value object of type '{0}' to an event of type '{1}'.\r\nSee inner exception for details.",
                    this.source.GetType(),
                    typeof(T)),
                ex);
        }

        return @event;
    }
}
