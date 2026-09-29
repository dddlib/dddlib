using System.Globalization;

namespace dddlib.Sdk.Configuration.Model;

/// <summary>
/// The runtime metadata for an entity type: its natural key (own or inherited) and its event mappings.
/// </summary>
public class EntityType
{
    private readonly NaturalKey? baseEntityNaturalKey;
    private NaturalKey? entityNaturalKey;

    public EntityType(Type runtimeType, ITypeAnalyzerService typeAnalyzerService)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        ArgumentNullException.ThrowIfNull(typeAnalyzerService);

        if (!typeAnalyzerService.IsValidEntity(runtimeType))
        {
            throw new BusinessException(
                string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not an entity.", runtimeType));
        }

        this.RuntimeType = runtimeType;
        this.entityNaturalKey = typeAnalyzerService.GetNaturalKey(runtimeType);
    }

    public EntityType(Type runtimeType, ITypeAnalyzerService typeAnalyzerService, EntityType baseEntity)
        : this(runtimeType, typeAnalyzerService)
    {
        ArgumentNullException.ThrowIfNull(baseEntity);

        if (baseEntity.RuntimeType != runtimeType.BaseType)
        {
            throw new BusinessException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The specified base entity runtime type '{0}' does not match the runtime type base type '{1}'.",
                    baseEntity.RuntimeType,
                    runtimeType.BaseType));
        }

        this.baseEntityNaturalKey = baseEntity.NaturalKey;
    }

    public Type RuntimeType { get; }

    public NaturalKey? NaturalKey => this.entityNaturalKey ?? this.baseEntityNaturalKey;

    public MapperCollection Mappings { get; } = new();

    public void ConfigureNaturalKey(NaturalKey naturalKey)
    {
        ArgumentNullException.ThrowIfNull(naturalKey);

        if (this.entityNaturalKey == naturalKey)
        {
            return;
        }

        if (naturalKey.RuntimeType != this.RuntimeType)
        {
            throw new BusinessException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Invalid natural key specified. The natural key must have a declaring type of '{0}'.",
                    this.RuntimeType));
        }

        if (this.entityNaturalKey is not null)
        {
            throw new BusinessException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Cannot configure the natural key for the entity '{0}' to be the property '{1}' as the natural key is already configured to be the property '{2}'.",
                    this.RuntimeType,
                    naturalKey.PropertyName,
                    this.entityNaturalKey.PropertyName));
        }

        this.entityNaturalKey = naturalKey;
    }
}
