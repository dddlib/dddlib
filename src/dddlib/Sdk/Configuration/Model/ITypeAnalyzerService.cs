namespace dddlib.Sdk.Configuration.Model;

/// <summary>
/// Analyzes runtime types to discover the metadata the runtime needs.
/// </summary>
public interface ITypeAnalyzerService
{
    bool IsValidAggregateRoot(Type runtimeType);

    bool IsValidEntity(Type runtimeType);

    bool IsValidValueObject(Type runtimeType);

    bool IsValidProperty(Type runtimeType, string propertyName, Type propertyType);

    NaturalKey? GetNaturalKey(Type runtimeType);

    Delegate? GetUninitializedFactory(Type runtimeType);
}
