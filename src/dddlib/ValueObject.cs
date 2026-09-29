using dddlib.Runtime;
using dddlib.Sdk.Configuration.Model;

namespace dddlib;

/// <summary>
/// Represents a value object: an object defined by its attributes rather than an identity. Two value objects of
/// the same runtime type are equal when the configured equality comparer says so. By default that is structural
/// equality over the public properties (see <see cref="Sdk.DefaultValueObjectEqualityComparer{T}"/>).
/// </summary>
/// <typeparam name="T">The type of the value object.</typeparam>
public abstract class ValueObject<T> : IEquatable<T>
    where T : ValueObject<T>
{
    private readonly IEqualityComparer<T> equalityComparer;

    protected ValueObject()
    {
        this.equalityComparer = GetEqualityComparer(Application.Current.GetValueObjectType(this.GetType()));
    }

    protected ValueObject(ValueObjectType valueObjectType)
    {
        ArgumentNullException.ThrowIfNull(valueObjectType);

        this.equalityComparer = GetEqualityComparer(valueObjectType);
    }

    public static bool operator ==(ValueObject<T>? first, ValueObject<T>? second) => object.Equals(first, second);

    public static bool operator !=(ValueObject<T>? first, ValueObject<T>? second) => !(first == second);

    public sealed override bool Equals(object? obj) => this.Equals(obj as T);

    public sealed override int GetHashCode() => this.equalityComparer.GetHashCode((T)this);

    public bool Equals(T? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(other, this))
        {
            return true;
        }

        if (other.GetType() != this.GetType())
        {
            return false;
        }

        return this.equalityComparer.Equals((T)this, other);
    }

    private static IEqualityComparer<T> GetEqualityComparer(ValueObjectType valueObjectType) =>
        (IEqualityComparer<T>)valueObjectType.EqualityComparer;
}
