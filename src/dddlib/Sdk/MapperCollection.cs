using System.Diagnostics.CodeAnalysis;

namespace dddlib.Sdk;

/// <summary>
/// The mappings configured for an entity or value object type, keyed by source type, destination type and shape.
/// </summary>
public sealed class MapperCollection
{
    private readonly Dictionary<MappingId, object> mappings = [];

    public void AddOrUpdate<TSource, TDestination>(Func<TSource, TDestination> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        this.mappings[new MappingId(typeof(TSource), typeof(TDestination), IsAction: false)] = mapping;
    }

    public void AddOrUpdate<TSource, TDestination>(Action<TSource, TDestination> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        this.mappings[new MappingId(typeof(TSource), typeof(TDestination), IsAction: true)] = mapping;
    }

    public bool TryGet<TSource, TDestination>([NotNullWhen(true)] out Func<TSource, TDestination>? mapping)
    {
        if (this.mappings.TryGetValue(new MappingId(typeof(TSource), typeof(TDestination), IsAction: false), out var value))
        {
            mapping = (Func<TSource, TDestination>)value;
            return true;
        }

        mapping = null;
        return false;
    }

    public bool TryGet<TSource, TDestination>([NotNullWhen(true)] out Action<TSource, TDestination>? mapping)
    {
        if (this.mappings.TryGetValue(new MappingId(typeof(TSource), typeof(TDestination), IsAction: true), out var value))
        {
            mapping = (Action<TSource, TDestination>)value;
            return true;
        }

        mapping = null;
        return false;
    }

    private readonly record struct MappingId(Type Source, Type Destination, bool IsAction);
}
