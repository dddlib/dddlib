using System.Globalization;
using dddlib.Runtime;

namespace dddlib.Sdk;

/// <summary>
/// Maps an entity or a value object to an event with the mappings configured for its type: one that creates the
/// event, or one that maps to an event that exists, by changing it or by returning a copy of it.
/// </summary>
internal static class EventMapping
{
    public const string Entity = "entity";
    public const string ValueObject = "value object";

    private const string HelpLink = "https://github.com/dddlib/dddlib/blob/main/docs/bootstrapper.md#mapping";

    public static TEvent ToNewEvent<TSource, TEvent>(MapperCollection mappings, TSource source, string kind)
        where TSource : notnull
    {
        if (mappings.TryGet<TSource, TEvent>(out Func<TSource, TEvent>? create))
        {
            try
            {
                return create(source);
            }
            catch (Exception ex) when (ex is not RuntimeException)
            {
                throw Failed<TEvent>(source, kind, ex);
            }
        }

        if (!mappings.TryGet<TSource, TEvent>(out Func<TSource, TEvent, TEvent>? _) && !mappings.TryGet<TSource, TEvent>(out Action<TSource, TEvent>? _))
        {
            throw NotConfigured<TEvent>(source, kind);
        }

        // Those mappings need an event to map to, which only its parameterless constructor can supply.
        if (!typeof(TEvent).IsValueType && (typeof(TEvent).IsAbstract || typeof(TEvent).GetConstructor(Type.EmptyTypes) is null))
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The {0} of type '{1}' cannot be mapped to a new event of type '{2}' as the event does not have a public parameterless constructor.
To fix this issue, either:
- use a bootstrapper to register a mapping that creates the event, or
- add a public parameterless constructor to the event.",
                    kind,
                    source.GetType(),
                    typeof(TEvent)))
            {
                HelpLink = HelpLink,
            };
        }

        return ToEvent(mappings, source, Activator.CreateInstance<TEvent>(), kind);
    }

    public static TEvent ToEvent<TSource, TEvent>(MapperCollection mappings, TSource source, TEvent @event, string kind)
        where TSource : notnull
    {
        Action<TSource, TEvent>? change = null;
        if (!mappings.TryGet<TSource, TEvent>(out Func<TSource, TEvent, TEvent>? copy) && !mappings.TryGet(out change))
        {
            if (!mappings.TryGet<TSource, TEvent>(out Func<TSource, TEvent>? _))
            {
                throw NotConfigured<TEvent>(source, kind);
            }

            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The {0} of type '{1}' has been configured to map to a new event of type '{2}', not to one that already exists.
To fix this issue, either:
- use a bootstrapper to register a mapping that takes the event, or
- map to the event without specifying one.",
                    kind,
                    source.GetType(),
                    typeof(TEvent)))
            {
                HelpLink = HelpLink,
            };
        }

        try
        {
            if (copy is not null)
            {
                return copy(source, @event);
            }

            change!(source, @event);
            return @event;
        }
        catch (Exception ex) when (ex is not RuntimeException)
        {
            throw Failed<TEvent>(source, kind, ex);
        }
    }

    private static RuntimeException NotConfigured<TEvent>(object source, string kind) =>
        new(
            string.Format(
                CultureInfo.InvariantCulture,
                @"The {0} of type '{1}' has not been configured to map to an event of type '{2}'.
To fix this issue:
- use a bootstrapper to register a mapping for the event.",
                kind,
                source.GetType(),
                typeof(TEvent)))
        {
            HelpLink = HelpLink,
        };

    private static RuntimeException Failed<TEvent>(object source, string kind, Exception exception) =>
        new(
            string.Format(
                CultureInfo.InvariantCulture,
                "An exception occurred whilst attempting to map {0} of type '{1}' to an event of type '{2}'.\r\nSee inner exception for details.",
                kind == Entity ? "an entity" : "a value object",
                source.GetType(),
                typeof(TEvent)),
            exception);
}
