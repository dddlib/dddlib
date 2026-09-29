using System.Globalization;
using System.Linq.Expressions;

namespace dddlib.Sdk.Configuration.Model;

/// <summary>
/// Describes the natural key of an entity type: the declaring type, the property name and the property type.
/// Two natural keys are equal when all three match.
/// </summary>
public sealed class NaturalKey : IEquatable<NaturalKey>
{
    private readonly Func<Entity, object?> getValue;

    public NaturalKey(Type runtimeType, string propertyName, Type propertyType, ITypeAnalyzerService typeAnalyzerService)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        ArgumentNullException.ThrowIfNull(propertyType);
        ArgumentNullException.ThrowIfNull(typeAnalyzerService);

        if (!typeAnalyzerService.IsValidEntity(runtimeType))
        {
            throw new BusinessException(
                string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not an entity.", runtimeType));
        }

        if (!typeAnalyzerService.IsValidProperty(runtimeType, propertyName, propertyType))
        {
            throw new BusinessException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Invalid natural key specification. The entity '{0}' does not contain a property named '{1}' of type '{2}'.",
                    runtimeType,
                    propertyName,
                    propertyType));
        }

        this.RuntimeType = runtimeType;
        this.PropertyName = propertyName;
        this.PropertyType = propertyType;
        this.getValue = CompileGetValue(runtimeType, propertyName);
    }

    public Type RuntimeType { get; }

    public string PropertyName { get; }

    public Type PropertyType { get; }

    public static bool operator ==(NaturalKey? left, NaturalKey? right) => object.Equals(left, right);

    public static bool operator !=(NaturalKey? left, NaturalKey? right) => !(left == right);

    public object? GetValue(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return this.getValue(entity);
    }

    public bool Equals(NaturalKey? other) =>
        other is not null &&
        this.RuntimeType == other.RuntimeType &&
        this.PropertyName == other.PropertyName &&
        this.PropertyType == other.PropertyType;

    public override bool Equals(object? obj) => this.Equals(obj as NaturalKey);

    public override int GetHashCode() => HashCode.Combine(this.RuntimeType, this.PropertyName, this.PropertyType);

    private static Func<Entity, object?> CompileGetValue(Type runtimeType, string propertyName)
    {
        var entity = Expression.Parameter(typeof(Entity), "entity");
        var property = Expression.Property(Expression.Convert(entity, runtimeType), propertyName);
        var body = Expression.Convert(property, typeof(object));

        return Expression.Lambda<Func<Entity, object?>>(body, entity).Compile();
    }
}
