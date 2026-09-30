using System.Diagnostics.CodeAnalysis;

namespace dddlib.Sdk;

/// <summary>
/// The mappings configured for an entity or value object type, keyed by source type, destination type and shape.
/// </summary>
public sealed class MapperCollection
{
    private readonly Dictionary<Type, Delegate> mappings = [];

    public void AddOrUpdate<TSource, TDestination>(Func<TSource, TDestination> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        this.mappings[typeof(Func<TSource, TDestination>)] = mapping;
    }

    public void AddOrUpdate<TSource, TDestination>(Action<TSource, TDestination> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        // A destination that exists is mapped to in one way, the one configured last: changed, or returned as a copy.
        this.mappings.Remove(typeof(Func<TSource, TDestination, TDestination>));
        this.mappings[typeof(Action<TSource, TDestination>)] = mapping;
    }

    public void AddOrUpdate<TSource, TDestination>(Func<TSource, TDestination, TDestination> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        this.mappings.Remove(typeof(Action<TSource, TDestination>));
        this.mappings[typeof(Func<TSource, TDestination, TDestination>)] = mapping;
    }

    public bool TryGet<TSource, TDestination>([NotNullWhen(true)] out Func<TSource, TDestination>? mapping) =>
        this.TryGetMapping(out mapping);

    public bool TryGet<TSource, TDestination>([NotNullWhen(true)] out Action<TSource, TDestination>? mapping) =>
        this.TryGetMapping(out mapping);

    public bool TryGet<TSource, TDestination>([NotNullWhen(true)] out Func<TSource, TDestination, TDestination>? mapping) =>
        this.TryGetMapping(out mapping);

    private bool TryGetMapping<TMapping>([NotNullWhen(true)] out TMapping? mapping)
        where TMapping : Delegate
    {
        mapping = this.mappings.TryGetValue(typeof(TMapping), out var value) ? (TMapping)value : null;
        return mapping is not null;
    }
}
