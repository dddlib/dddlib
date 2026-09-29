using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using dddlib.Runtime;

namespace dddlib.Sdk;

/// <summary>
/// Structural equality over the public readable properties of a value object, compiled once per type, following
/// the rules in <see cref="ValueObjectEquality"/>. This is the fallback for types the source generator does not cover.
/// </summary>
public sealed class DefaultValueObjectEqualityComparer<T> : IEqualityComparer<T>
    where T : ValueObject<T>
{
    private static readonly MethodInfo ValuesEqualMethod = typeof(ValueObjectEquality).GetMethod(nameof(ValueObjectEquality.ValuesEqual))!;
    private static readonly MethodInfo CombineHashMethod = typeof(ValueObjectEquality).GetMethod(nameof(ValueObjectEquality.CombineHash))!;

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
            (Expression)Expression.Constant(ValueObjectEquality.HashSeed),
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
}
