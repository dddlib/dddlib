using System.Globalization;
using System.Runtime.CompilerServices;
using dddlib.Runtime;
using dddlib.Sdk.Configuration.Model;

namespace dddlib;

/// <summary>
/// Represents an entity: an object defined by its identity (natural key) rather than its attributes.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    private readonly Func<Entity, object?>? getNaturalKeyValue;
    private bool isDestroyed;

    protected Entity()
        : this(static type => Application.Current.GetEntityType(type))
    {
    }

    protected Entity(EntityType entityType)
        : this(_ => entityType ?? throw new ArgumentNullException(nameof(entityType)))
    {
    }

    private protected Entity(Func<Type, EntityType> resolveEntityType)
    {
        this.Metadata = resolveEntityType(this.GetType());
        this.getNaturalKeyValue = this.Metadata.NaturalKey is { } naturalKey ? naturalKey.GetValue : null;
    }

    /// <summary>
    /// Gets a value indicating whether the lifecycle of this entity has ended.
    /// </summary>
    protected internal bool IsDestroyed => this.isDestroyed;

    private protected EntityType Metadata { get; }

    public static bool operator ==(Entity? first, Entity? second) => object.Equals(first, second);

    public static bool operator !=(Entity? first, Entity? second) => !(first == second);

    public sealed override bool Equals(object? obj) => this.Equals(obj as Entity);

    public sealed override int GetHashCode()
    {
        if (this.getNaturalKeyValue is null)
        {
            return RuntimeHelpers.GetHashCode(this);
        }

        return this.getNaturalKeyValue(this)?.GetHashCode() ?? 0;
    }

    public bool Equals(Entity? other)
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

        if (this.getNaturalKeyValue is null)
        {
            return false;
        }

        return object.Equals(this.getNaturalKeyValue(this), this.getNaturalKeyValue(other));
    }

    /// <summary>
    /// Throws a <see cref="BusinessException"/> if the lifecycle of this entity has ended.
    /// </summary>
    protected void ThrowIfLifecycleEnded() => this.ThrowIfLifecycleEnded(null);

    /// <summary>
    /// Throws a <see cref="BusinessException"/> if the lifecycle of this entity has ended.
    /// </summary>
    /// <param name="eventName">The name of the event that was being applied, for the exception message.</param>
    protected void ThrowIfLifecycleEnded(string? eventName)
    {
        if (!this.isDestroyed)
        {
            return;
        }

        var naturalKeyValue = this.getNaturalKeyValue?.Invoke(this);

        var format = string.IsNullOrEmpty(eventName)
            ? naturalKeyValue is null
                ? "Cannot apply changes because the '{0}' no longer exists in the system."
                : "Cannot apply changes to '{1}' because that '{0}' no longer exists in the system."
            : naturalKeyValue is null
                ? "Cannot apply '{2}' because the '{0}' no longer exists in the system."
                : "Cannot apply '{2}' to '{1}' because that '{0}' no longer exists in the system.";

        throw new BusinessException(
            string.Format(CultureInfo.InvariantCulture, format, this.GetType().Name, naturalKeyValue, eventName));
    }

    /// <summary>
    /// Ends the lifecycle of this entity. Subsequent calls to <see cref="ThrowIfLifecycleEnded()"/> will throw.
    /// </summary>
    protected void EndLifecycle()
    {
        this.ThrowIfLifecycleEnded();
        this.isDestroyed = true;
    }
}
