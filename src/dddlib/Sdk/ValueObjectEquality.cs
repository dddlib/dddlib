using System.Collections;
using System.ComponentModel;

namespace dddlib.Sdk;

/// <summary>
/// The member comparison rules shared by <see cref="DefaultValueObjectEqualityComparer{T}"/> and the comparers the
/// source generator emits: enumerable members (other than strings) compare element by element, everything else
/// through <see cref="EqualityComparer{T}.Default"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ValueObjectEquality
{
    public const int HashSeed = 17;

    public static bool ValuesEqual<TValue>(TValue left, TValue right)
    {
        if (left is IEnumerable leftItems and not string && right is IEnumerable rightItems and not string)
        {
            return leftItems.Cast<object?>().SequenceEqual(rightItems.Cast<object?>());
        }

        return EqualityComparer<TValue>.Default.Equals(left, right);
    }

    public static int CombineHash<TValue>(int seed, TValue value)
    {
        unchecked
        {
            if (value is IEnumerable items and not string)
            {
                return items.Cast<object?>().Aggregate(seed, static (hash, item) => (hash * 23) + (item?.GetHashCode() ?? 0));
            }

            return (seed * 23) + (value is null ? 0 : EqualityComparer<TValue>.Default.GetHashCode(value));
        }
    }
}
