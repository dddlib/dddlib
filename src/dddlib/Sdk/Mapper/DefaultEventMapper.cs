using System.Globalization;
using dddlib.Runtime;

namespace dddlib.Sdk;

internal sealed class DefaultEventMapper<TEvent> : IEventMapper<TEvent>
    where TEvent : notnull
{
    private readonly TEvent source;

    public DefaultEventMapper(TEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);

        this.source = source;
    }

    public T ToEntity<T>()
        where T : Entity
    {
        var runtimeType = Application.Current.GetEntityType(typeof(T));
        if (!runtimeType.Mappings.TryGet<TEvent, T>(out Func<TEvent, T>? mapping))
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The entity of type '{0}' has not been configured to reverse map from an event of type '{1}'.
To fix this issue:
- use a bootstrapper to register a reverse mapping for the event.",
                    typeof(T),
                    this.source.GetType()))
            {
                HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Entity-Mapping",
            };
        }

        return Invoke(mapping, this.source, "entity");
    }

    public T ToValueObject<T>()
        where T : notnull
    {
        var runtimeType = Application.Current.GetValueObjectType(typeof(T));
        if (!runtimeType.Mappings.TryGet<TEvent, T>(out Func<TEvent, T>? mapping))
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The value object of type '{0}' has not been configured to reverse map from an event of type '{1}'.
To fix this issue:
- use a bootstrapper to register a reverse mapping for the event.",
                    typeof(T),
                    this.source.GetType()))
            {
                HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Value-Object-Mapping",
            };
        }

        return Invoke(mapping, this.source, "value object");
    }

    private static T Invoke<T>(Func<TEvent, T> mapping, TEvent source, string destinationKind)
    {
        try
        {
            return mapping(source);
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
                    "An exception occurred whilst attempting to map an event of type '{0}' to {1} of type '{2}'.\r\nSee inner exception for details.",
                    source.GetType(),
                    destinationKind,
                    typeof(T)),
                ex);
        }
    }
}
