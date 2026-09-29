using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace dddlib.Generators;

internal sealed record ContainerModel(string Keyword, string Name);

internal sealed record DomainTypeModel(
    string? Namespace,
    EquatableArray<ContainerModel> Containers,
    string Keyword,
    string Name,
    string FullyQualifiedName,
    string HintName,
    DomainTypeKind Kind,
    string? NaturalKeyPropertyName,
    string? NaturalKeyPropertyType,
    bool HasUninitializedFactory,
    EquatableArray<string> HandlerEventTypes,
    string? ValueObjectArgument,
    EquatableArray<string> ValueObjectProperties);

/// <summary>
/// Emits a nested <c>__DddlibMetadata</c> class into every partial aggregate root, entity and value object:
/// exact-type event dispatch, natural key access, reconstitution factory and value object equality, so the runtime
/// needs no reflection for those types.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DomainTypeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null } declaration && declaration.Modifiers.Any(SyntaxKind.PartialKeyword),
                static (syntaxContext, cancellationToken) => Transform(syntaxContext, cancellationToken))
            .Where(static model => model is not null);

        context.RegisterSourceOutput(models, static (productionContext, model) => productionContext.AddSource(model!.HintName, SourceEmitter.Emit(model)));
    }

    private static DomainTypeModel? Transform(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken) is not INamedTypeSymbol symbol || symbol.IsGenericType)
        {
            return null;
        }

        // Only the first declaration of a partial type produces output.
        if (symbol.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) != context.Node)
        {
            return null;
        }

        var known = KnownSymbols.Create(context.SemanticModel.Compilation);
        if (known is null || !symbol.IsPartialIncludingContainers())
        {
            return null;
        }

        var kind = known.GetDomainTypeKind(symbol);
        if (kind == DomainTypeKind.None)
        {
            return null;
        }

        var containers = new List<ContainerModel>();
        for (var container = symbol.ContainingType; container is not null; container = container.ContainingType)
        {
            containers.Insert(0, new ContainerModel(container.GetDeclarationKeyword(), container.GetNameWithTypeParameters()));
        }

        var fullyQualifiedName = symbol.ToDisplayString(SymbolExtensions.FullyQualified);
        var hintName = string.Concat(symbol.ToDisplayString().Replace('<', '_').Replace('>', '_').Replace('+', '.'), ".dddlib.g.cs");
        var @namespace = symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString();

        string? naturalKeyName = null;
        string? naturalKeyType = null;
        var hasFactory = false;
        var handlerEventTypes = EquatableArray<string>.Empty;
        string? valueObjectArgument = null;
        var valueObjectProperties = EquatableArray<string>.Empty;

        if (kind is DomainTypeKind.AggregateRoot or DomainTypeKind.Entity)
        {
            var naturalKeys = known.GetDeclaredNaturalKeyProperties(symbol).ToList();
            if (naturalKeys.Count > 1)
            {
                // The analyzer reports this; the runtime reflection path throws as before.
                return null;
            }

            if (naturalKeys.Count == 1)
            {
                naturalKeyName = naturalKeys[0].Name;
                naturalKeyType = naturalKeys[0].Type.ToNonNullableDisplayString();
            }
        }

        if (kind == DomainTypeKind.AggregateRoot)
        {
            hasFactory = symbol.HasParameterlessConstructor();
            handlerEventTypes = symbol.GetDispatchableHandlers()
                .Select(handler => handler.Parameters[0].Type.ToNonNullableDisplayString())
                .Distinct(StringComparer.Ordinal)
                .ToEquatableArray();
        }

        if (kind == DomainTypeKind.ValueObject)
        {
            var properties = known.GetValueObjectProperties(symbol).Select(property => property.Name).ToList();
            if (properties.Count == 0 || known.GetValueObjectArgument(symbol) is not { } argument)
            {
                // No public properties: the runtime default comparer reports the problem.
                return null;
            }

            valueObjectArgument = argument.ToDisplayString(SymbolExtensions.FullyQualified);
            valueObjectProperties = properties.ToEquatableArray();
        }

        return new DomainTypeModel(
            @namespace,
            containers.ToEquatableArray(),
            symbol.GetDeclarationKeyword(),
            symbol.GetNameWithTypeParameters(),
            fullyQualifiedName,
            hintName,
            kind,
            naturalKeyName,
            naturalKeyType,
            hasFactory,
            handlerEventTypes,
            valueObjectArgument,
            valueObjectProperties);
    }
}
