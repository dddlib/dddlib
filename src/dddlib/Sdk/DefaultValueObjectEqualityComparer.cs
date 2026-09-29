using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using dddlib.Runtime;

namespace dddlib.Sdk;

/// <summary>
/// Structural equality over the public readable properties of a value object, compiled once per type.
/// Properties that are <see cref="IEnumerable"/> (other than <see cref="string"/>) compare element by element;
/// everything else compares through <see cref="EqualityComparer{T}.Default"/>, so nested value objects and
/// types that override <c>Equals</c> behave as expected. Hash codes follow the same rules.
/// </summary>
public sealed class DefaultValueObjectEqualityComparer<T> : IEqualityComparer<T>
    where T : ValueObject<T>
{
    private static readonly MethodInfo ValuesEqualMethod = typeof(DefaultValueObjectEqualityComparer<T>)
        .GetMethod(nameof(ValuesEqual), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo CombineHashMethod = typeof(DefaultValueObjectEqualityComparer<T>)
        .GetMethod(nameof(CombineHash), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly Func<T, T, bool> equals;
    private readonly Func<T, int> hashCode;

    public DefaultValueObjectEqualityComparer()
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0)
            .ToArray();

        if (properties.Length == 0)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The value object of type '{0}' does not have any public properties and is configured to use the default value object equality comparer. In this configuration no value object instance will ever be equal.
To fix this issue, either:
- add one or more public properties to the value object, or
- define a custom value object equality comparer in a bootstrapper.",
                    typeof(T)))
            {
                HelpLink = "https://github.com/dddlib/dddlib/wiki/Value-Object-Equality",
            };
        }

        var left = Expression.Parameter(typeof(T), "left");
        var right = Expression.Parameter(typeof(T), "right");
        var equalsBody = properties
            .Select(property => (Expression)Expression.Call(
                ValuesEqualMethod.MakeGenericMethod(property.PropertyType),
                Expression.Property(left, property),
                Expression.Property(right, property)))
            .Aggregate(Expression.AndAlso);

        this.equals = Expression.Lambda<Func<T, T, bool>>(equalsBody, left, right).Compile();

        var obj = Expression.Parameter(typeof(T), "obj");
        var hashCodeBody = properties.Aggregate(
            (Expression)Expression.Constant(17),
            (current, property) => Expression.Call(
                CombineHashMethod.MakeGenericMethod(property.PropertyType),
                current,
                Expression.Property(obj, property)));

        this.hashCode = Expression.Lambda<Func<T, int>>(hashCodeBody, obj).Compile();
    }

    public bool Equals(T? x, T? y)
    {
        if (x is null || y is null)
        {
            return x is null && y is null;
        }

        return ReferenceEquals(x, y) || this.equals(x, y);
    }

    public int GetHashCode(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        return this.hashCode(obj);
    }

    private static bool ValuesEqual<TValue>(TValue left, TValue right)
    {
        if (left is IEnumerable leftItems and not string && right is IEnumerable rightItems and not string)
        {
            return leftItems.Cast<object?>().SequenceEqual(rightItems.Cast<object?>());
        }

        return EqualityComparer<TValue>.Default.Equals(left, right);
    }

    private static int CombineHash<TValue>(int seed, TValue value)
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
