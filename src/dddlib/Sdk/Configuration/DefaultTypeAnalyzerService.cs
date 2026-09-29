using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using dddlib.Runtime;
using dddlib.Sdk.Generated;
using dddlib.Sdk.Configuration.Model;

namespace dddlib.Sdk.Configuration;

public sealed class DefaultTypeAnalyzerService : ITypeAnalyzerService
{
    private const BindingFlags DeclaredPublicInstance = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public;

    public bool IsValidAggregateRoot(Type runtimeType) => typeof(AggregateRoot).IsAssignableFrom(runtimeType);

    public bool IsValidEntity(Type runtimeType) => typeof(Entity).IsAssignableFrom(runtimeType);

    public bool IsValidValueObject(Type runtimeType) => runtimeType.IsSubclassOfRawGeneric(typeof(ValueObject<>));

    public bool IsValidProperty(Type runtimeType, string propertyName, Type propertyType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);

        return runtimeType.GetProperties(DeclaredPublicInstance)
            .Any(property => property.Name == propertyName && property.PropertyType == propertyType);
    }

    public NaturalKey? GetNaturalKey(Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);

        // Generated metadata is authoritative for the natural key declared on the type itself.
        if (GeneratedMetadata.TryGet<IGeneratedEntityMetadata>(runtimeType) is { } generated)
        {
            return generated is { NaturalKeyPropertyName: { } propertyName, NaturalKeyPropertyType: { } propertyType }
                ? new NaturalKey(runtimeType, propertyName, propertyType, generated.GetNaturalKeyValue)
                : null;
        }

        var naturalKeys = runtimeType.GetProperties(DeclaredPublicInstance)
            .Where(static property => property.GetCustomAttribute<NaturalKeyAttribute>(inherit: false) is not null)
            .ToArray();

        if (naturalKeys.Length > 1)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"Entity of type '{0}' has more than one natural key defined.
To fix this issue:
- ensure that there is only a single natural key defined for the entity.",
                    runtimeType))
            {
                HelpLink = "https://github.com/dddlib/dddlib/blob/main/docs/entity-equality.md",
            };
        }

        if (naturalKeys.Length == 0)
        {
            return null;
        }

        var naturalKey = naturalKeys[0];
        return new NaturalKey(naturalKey.DeclaringType!, naturalKey.Name, naturalKey.PropertyType, this);
    }

    public Delegate? GetUninitializedFactory(Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);

        if (GeneratedMetadata.TryGet<IGeneratedAggregateRootMetadata>(runtimeType) is { } generated)
        {
            return generated.UninitializedFactory;
        }

        if (runtimeType.IsAbstract)
        {
            return null;
        }

        var defaultConstructor = runtimeType.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);

        if (defaultConstructor is null)
        {
            return null;
        }

        var body = Expression.New(defaultConstructor);
        var factoryType = typeof(Func<>).MakeGenericType(runtimeType);

        return Expression.Lambda(factoryType, body).Compile();
    }
}
