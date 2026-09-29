using System.Globalization;
using dddlib.Runtime;

namespace dddlib.Sdk;

internal sealed class DefaultEntityMapper<TEntity> : IEntityMapper<TEntity>
    where TEntity : Entity
{
    private readonly TEntity source;

    public DefaultEntityMapper(TEntity source)
    {
        ArgumentNullException.ThrowIfNull(source);

        this.source = source;
    }

    public T ToEvent<T>()
        where T : new()
        => this.ToEvent(new T());

    public T ToEvent<T>(T @event)
    {
        var runtimeType = Application.Current.GetEntityType(this.source.GetType());
        if (!runtimeType.Mappings.TryGet<TEntity, T>(out Action<TEntity, T>? mapping))
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The entity of type '{0}' has not been configured to map to an event of type '{1}'.
To fix this issue:
- use a bootstrapper to register a mapping for the event.",
                    this.source.GetType(),
                    typeof(T)))
            {
                HelpLink = "https://github.com/dddlib/dddlib/blob/main/docs/bootstrapper.md#mapping",
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
                    "An exception occurred whilst attempting to map an entity of type '{0}' to an event of type '{1}'.\r\nSee inner exception for details.",
                    this.source.GetType(),
                    typeof(T)),
                ex);
        }

        return @event;
    }
}
