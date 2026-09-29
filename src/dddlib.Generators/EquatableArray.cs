using System.Collections;
using System.Collections.Immutable;

namespace dddlib.Generators;

/// <summary>
/// An immutable array with structural equality, so generator models stay cacheable across incremental runs.
/// </summary>
internal readonly struct EquatableArray<T>(ImmutableArray<T> values) : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> values = values.IsDefault ? ImmutableArray<T>.Empty : values;

    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    public int Count => this.values.Length;

    public T this[int index] => this.values[index];

    public bool Equals(EquatableArray<T> other) => this.values.AsSpan().SequenceEqual(other.values.AsSpan());

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && this.Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var value in this.values)
            {
                hash = (hash * 23) + (value?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)this.values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}

internal static class EquatableArray
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> values)
        where T : IEquatable<T>
        => new(values.ToImmutableArray());
}
